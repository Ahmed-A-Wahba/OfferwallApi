using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using OfferwallApi.Infrastructure.Authorization;
using OfferwallApi.Infrastructure.Interfaces;
using OfferwallApi.Infrastructure.Persistence;
using OfferwallApi.Infrastructure.Results;
using OfferwallApi.Shared.Entities;

namespace OfferwallApi.Features.User.Codes;

public static class RefundCoupon
{
    [Authorize(Role = "User")]
    public sealed record Command(Guid CouponId) : IRequest<Result<Response>>;

    public sealed record Response(
        Guid CouponId,
        decimal RefundedPoints,
        decimal CurrentWalletBalanceUsd,
        string Status
    );

    public sealed class Handler(ApplicationDbContext dbContext, ICurrentUser currentUser)
        : IRequestHandler<Command, Result<Response>>
    {
        private static readonly TimeSpan RefundWindow = TimeSpan.FromHours(2);

        public async Task<Result<Response>> Handle(Command request, CancellationToken cancellationToken)
        {
            var userId = currentUser.UserId;

            // Fetch Coupon, User, Partner, and Wallet in an optimized single query structure
            var user = await dbContext.Users
                .Include(u => u.Partner)
                .Include(u => u.Wallet)
                .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);

            if (user is null || user.Wallet is null)
            {
                return Error.Failure("User.NotFound", "User or wallet was not found.");
            }

            var coupon = await dbContext.Coupons
                .FirstOrDefaultAsync(c => c.CouponId == request.CouponId && c.UserId == userId, cancellationToken);

            if (coupon is null)
            {
                return Error.Failure("Coupon.NotFound", "The coupon does not exist or does not belong to the user.");
            }

            if (coupon.Status == CouponStatus.Redeemed)
            {
                return Error.Failure("Coupon.AlreadyRedeemed", "The coupon cannot be refunded as it has already been redeemed.");
            }

            if (coupon.Status == CouponStatus.Cancelled)
            {
                return Error.Failure("Coupon.AlreadyCancelled", "The coupon has already been cancelled.");
            }

            var now = DateTime.UtcNow;
            if (now - coupon.CreatedAt > RefundWindow)
            {
                return Error.Failure("Coupon.RefundWindowExpired", "The refund window for this coupon has expired.");
            }

            // Financial Calculations
            decimal pointsPerUsd = user.Partner?.PointsPerUsd > 0 ? user.Partner.PointsPerUsd : 100m;

            // coupon.Value stores USD amount, calculate returned Points equivalent
            decimal refundedPoints = Math.Round(coupon.Value * pointsPerUsd, 2, MidpointRounding.AwayFromZero);

            var strategy = dbContext.Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync(async () =>
            {
                await using var dbTransaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

                try
                {
                    // 1. Update Coupon Status
                    coupon.Status = CouponStatus.Cancelled;
                    coupon.UpdatedAt = now;

                    // 2. Refund USD value to Wallet
                    user.Wallet.Balance += coupon.Value;
                    user.Wallet.UpdatedAt = now;

                    // 3. Log Wallet Transaction
                    var transaction = new WalletTransaction
                    {
                        TransactionId = Guid.CreateVersion7(),
                        UserId = userId,
                        Amount = coupon.Value, // Positive USD refund
                        Type = WalletTransactionType.CouponCancellation,
                        CreatedAt = now
                    };

                    dbContext.WalletTransactions.Add(transaction);

                    await dbContext.SaveChangesAsync(cancellationToken);
                    await dbTransaction.CommitAsync(cancellationToken);

                    return Result<Response>.Success(new Response(
                        coupon.CouponId,
                        refundedPoints,
                        user.Wallet.Balance,
                        coupon.Status.ToString()
                    ));
                }
                catch
                {
                    await dbTransaction.RollbackAsync(cancellationToken);
                    throw;
                }
            });
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapPost("/user/coupons/{id:guid}/refund", async (
                [FromRoute] Guid id,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var command = new Command(id);
                var result = await sender.Send(command, cancellationToken);

                return result.ToHttpResponse();
            })
            .RequireAuthorization()
            .WithTags("User Coupons");
        }
    }
}
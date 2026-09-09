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
        decimal CurrentWalletBalance,
        string Status
    );

    public sealed class Handler(ApplicationDbContext dbContext, ICurrentUser currentUser) : IRequestHandler<Command, Result<Response>>
    {
        private static readonly TimeSpan RefundWindow = TimeSpan.FromHours(2);

        public async Task<Result<Response>> Handle(Command request, CancellationToken cancellationToken)
        {
            var userId = currentUser.UserId;

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

            var timeElapsed = DateTime.UtcNow - coupon.CreatedAt;
            if (timeElapsed > RefundWindow)
            {
                return Error.Failure("Coupon.RefundWindowExpired", "The refund window for this coupon has expired.");
            }

            var wallet = await dbContext.Wallets
                .FirstOrDefaultAsync(w => w.UserId == userId, cancellationToken);

            if (wallet is null)
            {
                return Error.Failure("Wallet.NotFound", "The wallet does not exist.");
            }

            coupon.Status = CouponStatus.Cancelled;
            coupon.UpdatedAt = DateTime.UtcNow;

            wallet.Balance += coupon.Value;
            wallet.UpdatedAt = DateTime.UtcNow;

            var transaction = new WalletTransaction
            {
                TransactionId = Guid.NewGuid(),
                UserId = userId,
                Amount = coupon.Value,
                Type = WalletTransactionType.CouponCancellation,
                CreatedAt = DateTime.UtcNow
            };

            dbContext.WalletTransactions.Add(transaction);

            await dbContext.SaveChangesAsync(cancellationToken);

            var response = new Response(
                coupon.CouponId,
                coupon.Value,
                wallet.Balance,
                coupon.Status.ToString()
            );

            return response;
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapPost("/user/coupons/{id:guid}/refund", async (
                [FromRoute] Guid id,
                ISender sender = null!,
                CancellationToken cancellationToken = default) =>
            {
                var command = new Command(id);
                var result = await sender.Send(command, cancellationToken);

                return result.ToHttpResponse();
            }).RequireAuthorization();
        }
    }
}
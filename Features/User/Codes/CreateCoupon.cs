using Carter;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfferwallApi.Infrastructure.Authorization;
using OfferwallApi.Infrastructure.Interfaces;
using OfferwallApi.Infrastructure.Persistence;
using OfferwallApi.Infrastructure.Results;
using OfferwallApi.Shared.Entities;

namespace OfferwallApi.Features.User.Codes;

public static class CreateCoupon
{
    [Authorize(Role = "User")]
    public sealed record Command(decimal Points) : IRequest<Result<Response>>;

    public sealed record Response(
        Guid CouponId,
        string Code,
        decimal PointsValue,
        decimal UsdValue,
        string Status,
        DateTime CreatedAt
    );

    public sealed class Handler(ApplicationDbContext dbContext, ICurrentUser currentUser, IUserLocationService userLocationService) : IRequestHandler<Command, Result<Response>>
    {
        public async Task<Result<Response>> Handle(Command request, CancellationToken cancellationToken)
        {
            var userId = currentUser.UserId;

            var user = await dbContext.Users
                .Include(u => u.Partner)
                .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);

            if (user is null)
            {
                return Error.Failure("User.NotFound", "User is not found.");
            }

            var wallet = await dbContext.Wallets
                .FirstOrDefaultAsync(w => w.UserId == userId, cancellationToken);

            if (wallet is null || wallet.Balance < request.Points)
            {
                return Error.Failure("Wallet.InsufficientBalance", "Insufficient points balance for redemption.");
            }

            if (request.Points <= 0)
            {
                return Error.Failure("Coupon.InvalidAmount", "Points amount must be greater than zero.");
            }

            if (request.Points > wallet.Balance)
            {
                return Error.Failure("Coupon.ExceedsBalance", "Requested points exceed available balance.");
            }

            var country = await userLocationService.GetCountryCodeAsync(cancellationToken);

            wallet.Balance -= request.Points;
            wallet.UpdatedAt = DateTime.UtcNow;

            decimal pointsPerUsd = user.Partner.PointsPerUsd > 0 ? user.Partner.PointsPerUsd : 100m;
            decimal usdValue = Math.Round(request.Points / pointsPerUsd, 2);

            var generatedCode = $"CPN-{Guid.NewGuid().ToString()[..8].ToUpper()}";



            var coupon = new Coupon
            {
                CouponId = Guid.CreateVersion7(),
                UserId = userId,
                PartnerId = user.PartnerId,
                Code = generatedCode,
                Value = request.Points,
                CountryCode = country ?? "EG",
                CityCode = "Cairo",
                Status = CouponStatus.Active,
                ExpiresAt = DateTime.UtcNow.AddDays(30),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var transaction = new WalletTransaction
            {
                TransactionId = Guid.CreateVersion7(),
                UserId = userId,
                Amount = -request.Points,
                Type = WalletTransactionType.CouponCreation,
                CreatedAt = DateTime.UtcNow
            };

            dbContext.Coupons.Add(coupon);
            dbContext.WalletTransactions.Add(transaction);

            await dbContext.SaveChangesAsync(cancellationToken);

            var response = new Response(
                coupon.CouponId,
                coupon.Code,
                coupon.Value,
                usdValue,
                coupon.Status.ToString(),
                coupon.CreatedAt
            );

            return response;
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapPost("/user/coupons/redeem", async (
                [FromBody] Command command,
                ISender sender = null!,
                CancellationToken cancellationToken = default) =>
            {
                var result = await sender.Send(command, cancellationToken);

                return result.ToHttpResponse();
            }).RequireAuthorization();
        }
    }
}
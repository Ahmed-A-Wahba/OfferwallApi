using Carter;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfferwallApi.Infrastructure.Authorization;
using OfferwallApi.Infrastructure.Interfaces;
using OfferwallApi.Infrastructure.Persistence;
using OfferwallApi.Infrastructure.Results;
using OfferwallApi.Shared.Entities;
using System.Security.Cryptography;

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
        DateTime CreatedAt,
        DateTime ExpiresAt
    );

    // 1. Validator Class
    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.Points)
                .GreaterThan(0)
                .WithMessage("Points amount must be greater than zero.");
        }
    }

    // 2. Handler Class
    public sealed class Handler(
        ApplicationDbContext dbContext,
        ICurrentUser currentUser,
        IUserLocationService userLocationService)
        : IRequestHandler<Command, Result<Response>>
    {
        public async Task<Result<Response>> Handle(Command request, CancellationToken cancellationToken)
        {
            var userId = currentUser.UserId;

            // Fetch User with Partner and Wallet in a single query
            var user = await dbContext.Users
                .Include(u => u.Partner)
                .Include(u => u.Wallet)
                .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);

            if (user is null)
            {
                return Error.Failure("User.NotFound", "User was not found.");
            }

            if (user.Wallet is null)
            {
                return Error.Failure("Wallet.NotFound", "User wallet is not initialized.");
            }

            var partner = user.Partner;
            if (partner is null || partner.PointsPerUsd <= 0)
            {
                return Error.Failure("Partner.InvalidConfig", "Partner points configuration is invalid.");
            }

            // Convert requested points to USD Equivalent for Balance Validation
            decimal pointsPerUsd = partner.PointsPerUsd;
            decimal requiredUsdAmount = request.Points / pointsPerUsd;

            // Assuming Wallet.Balance is maintained in USD
            if (user.Wallet.Balance < requiredUsdAmount)
            {
                return Error.Failure("Wallet.InsufficientBalance", "Insufficient points balance for redemption.");
            }

            var countryCode = await userLocationService.GetCountryCodeAsync(cancellationToken);
            var now = DateTime.UtcNow;

            // Begin Execution Strategy / Transaction for Data Consistency
            var strategy = dbContext.Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync(async () =>
            {
                await using var dbTransaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

                try
                {
                    // Deduct USD equivalent from Wallet Balance
                    user.Wallet.Balance -= requiredUsdAmount;
                    user.Wallet.UpdatedAt = now;

                    decimal usdValue = Math.Round(requiredUsdAmount, 2, MidpointRounding.AwayFromZero);
                    string generatedCode = GenerateSecureCouponCode();

                    var coupon = new Coupon
                    {
                        CouponId = Guid.CreateVersion7(),
                        UserId = userId,
                        PartnerId = user.PartnerId,
                        Code = generatedCode,
                        Value = usdValue,
                        CountryCode = countryCode ?? "EG",
                        CityCode = "Cairo",
                        Status = CouponStatus.Active,
                        ExpiresAt = now.AddDays(30),
                        CreatedAt = now,
                        UpdatedAt = now
                    };

                    var transaction = new WalletTransaction
                    {
                        TransactionId = Guid.CreateVersion7(),
                        UserId = userId,
                        Amount = -requiredUsdAmount, // Track deduction in USD
                        Type = WalletTransactionType.CouponCreation,
                        CreatedAt = now
                    };

                    dbContext.Coupons.Add(coupon);
                    dbContext.WalletTransactions.Add(transaction);

                    await dbContext.SaveChangesAsync(cancellationToken);
                    await dbTransaction.CommitAsync(cancellationToken);

                    return Result<Response>.Success(new Response(
                        coupon.CouponId,
                        coupon.Code,
                        coupon.Value,
                        usdValue,
                        coupon.Status.ToString(),
                        coupon.CreatedAt,
                        coupon.ExpiresAt
                    ));
                }
                catch
                {
                    await dbTransaction.RollbackAsync(cancellationToken);
                    throw;
                }
            });
        }

        private static string GenerateSecureCouponCode()
        {
            var bytes = new byte[4];
            RandomNumberGenerator.Fill(bytes);
            var hex = Convert.ToHexString(bytes);
            return $"CPN-{hex}";
        }
    }

    // 3. Carter Endpoint
    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapPost("/user/coupons/redeem", async (
                [FromBody] Command command,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(command, cancellationToken);
                return result.ToHttpResponse();
            })
            .RequireAuthorization()
            .WithTags("User Coupons");
        }
    }
}
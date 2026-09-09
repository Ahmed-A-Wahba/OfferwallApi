using System.Globalization;
using Carter;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfferwallApi.Infrastructure.Persistence;
using OfferwallApi.Infrastructure.Results;
using OfferwallApi.Shared.Entities;

namespace OfferwallApi.Features.User.Offers;

public static class ProcessKlinkPostback
{
    public sealed record Command(
        string Payout,
        string Status,
        string UserId,
        string OfferId,
        string OfferName,
        string EventId,
        string EventName,
        string? TaskId,
        string EventType,
        string ConversionId,
        string? ReversedConversionId,
        string? K1, string? K2, string? K3, string? K4, string? K5
    ) : IRequest<Result<string>>;

    public sealed class Handler(ApplicationDbContext dbContext) : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            if (!Guid.TryParse(request.ConversionId, out var conversionId) ||
                !Guid.TryParse(request.UserId, out var userId))
            {
                return Error.Failure("InvalidConversionIdOrUserId", "Invalid ConversionId or UserId format.");
            }

            var conversionExists = await dbContext.Conversions
                .AnyAsync(c => c.ConversionId == conversionId, cancellationToken);

            if (conversionExists)
            {
                return Result.Success();
            }

            if (!decimal.TryParse(request.Payout, NumberStyles.Any, CultureInfo.InvariantCulture, out var payoutAmount))
            {
                return Error.Failure("InvalidPayoutAmount", "Invalid payout amount format.");
            }

            var wallet = await dbContext.Wallets
                .FirstOrDefaultAsync(w => w.UserId == userId, cancellationToken);

            if (wallet is null)
            {
                return Error.Failure("UserWalletNotFound", "User wallet not found.");
            }

            var user = await dbContext.Users
                .Include(u => u.Partner)
                .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);

            if (user is null || user.Partner is null)
            {
                return Error.Failure("UserOrPartnerNotFound", "User or associated Partner not found.");
            }

            var pointsAmount = payoutAmount * user.Partner.RevenueShareRatio * user.Partner.PointsPerUsd;

            var isConversion = request.EventType.Equals("conversion", StringComparison.OrdinalIgnoreCase) && request.Status == "completed";
            var isChargeback = request.EventType.Equals("chargeback", StringComparison.OrdinalIgnoreCase) && request.Status == "cancelled";

            if (!isConversion && !isChargeback)
            {
                return Error.Failure("UnknownEventTypeOrStatus", "Unknown event type or status.");
            }

            var conversionType = isConversion ? ConversionType.Conversion : ConversionType.Chargeback;
            var transactionType = isConversion ? WalletTransactionType.OfferReward : WalletTransactionType.Chargeback;

            Guid? reversedId = null;
            if (!string.IsNullOrEmpty(request.ReversedConversionId) && Guid.TryParse(request.ReversedConversionId, out var rId))
            {
                reversedId = rId;
            }

            var conversion = new Conversion
            {
                ConversionId = conversionId,
                UserId = userId,
                OfferId = request.OfferId,
                EventId = request.EventId,
                Payout = payoutAmount,
                Type = conversionType,
                ReversedConversionId = reversedId,
                CreatedAt = DateTime.UtcNow
            };
            dbContext.Conversions.Add(conversion);

            wallet.Balance += pointsAmount;
            wallet.UpdatedAt = DateTime.UtcNow;

            var transaction = new WalletTransaction
            {
                TransactionId = Guid.CreateVersion7(),
                UserId = userId,
                Amount = pointsAmount,
                Type = transactionType,
                ConversionId = conversionId,
                CreatedAt = DateTime.UtcNow
            };
            dbContext.WalletTransactions.Add(transaction);

            if (!string.IsNullOrEmpty(request.K1) && Guid.TryParse(request.K1, out var clickId))
            {
                var click = await dbContext.Clicks
                    .FirstOrDefaultAsync(c => c.ClickId == clickId, cancellationToken);

                if (click is not null && isConversion)
                {
                    click.Status = ClickStatus.Completed;
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        private static readonly string[] AllowedIps =
        {
            "34.118.33.53", "138.68.125.171", "64.226.93.56",
            "74.220.53.15", "74.220.53.234", "74.220.53.235"
        };

        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapPost("/api/webhooks/klink", async (
                HttpContext context,
                [FromBody] Command command,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var remoteIp = context.Connection.RemoteIpAddress?.ToString();

                if (string.IsNullOrEmpty(remoteIp) || !AllowedIps.Contains(remoteIp))
                {
                    return Results.Unauthorized();
                }

                var result = await sender.Send(command, cancellationToken);

                return result.IsSuccess ? Results.Ok() : Results.BadRequest(result.Error);
            })
            .AllowAnonymous();
        }
    }
}
using System.Globalization;
using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
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
        string? OfferName,
        string EventId,
        string? EventName,
        string? TaskId,
        string EventType,
        string ConversionId,
        string? ReversedConversionId,
        string? K1, string? K2, string? K3, string? K4, string? K5
    ) : IRequest<Result>;

    public sealed class Handler(ApplicationDbContext dbContext) : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            // 1. Validate Core Identifiers
            if (!Guid.TryParse(request.ConversionId, out var conversionId) ||
                !Guid.TryParse(request.UserId, out var userId))
            {
                return Error.Failure("InvalidConversionIdOrUserId", "Invalid ConversionId or UserId format.");
            }

            // 2. Idempotency Check: Prevent duplicate processing
            var conversionExists = await dbContext.Conversions
                .AsNoTracking()
                .AnyAsync(c => c.ConversionId == conversionId, cancellationToken);

            if (conversionExists)
            {
                return Result.Success(); // Return Success to Klink so they acknowledge delivery
            }

            // 3. Parse Payout (Handles negative values like "-5.00" directly)
            if (!decimal.TryParse(request.Payout, NumberStyles.Any, CultureInfo.InvariantCulture, out var rawPayoutAmount))
            {
                return Error.Failure("InvalidPayoutAmount", "Invalid payout amount format.");
            }

            // 4. Validate Event Types & Statuses matching Klink Spec
            var isConversion = request.EventType.Equals("conversion", StringComparison.OrdinalIgnoreCase) &&
                               request.Status.Equals("completed", StringComparison.OrdinalIgnoreCase);

            var isChargeback = request.EventType.Equals("chargeback", StringComparison.OrdinalIgnoreCase) &&
                               request.Status.Equals("cancelled", StringComparison.OrdinalIgnoreCase);

            if (!isConversion && !isChargeback)
            {
                return Error.Failure("UnknownEventTypeOrStatus", "Unknown event type or status.");
            }

            // 5. Fetch User, Partner, and Wallet
            var user = await dbContext.Users
                .Include(u => u.Partner)
                .Include(u => u.Wallet)
                .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);

            if (user is null || user.Wallet is null || user.Partner is null)
            {
                return Error.Failure("UserOrPartnerNotFound", "User, Wallet, or associated Partner was not found.");
            }

            // Calculate Partner's Net USD Share (Absolute base to avoid double negatives)
            decimal absolutePayout = Math.Abs(rawPayoutAmount);
            decimal partnerUsdShare = Math.Round(absolutePayout * user.Partner.RevenueShareRatio, 4, MidpointRounding.AwayFromZero);

            // Apply negative sign for Chargebacks
            if (isChargeback)
            {
                partnerUsdShare = -partnerUsdShare;
            }

            var conversionType = isConversion ? ConversionType.Conversion : ConversionType.Chargeback;
            var transactionType = isConversion ? WalletTransactionType.OfferReward : WalletTransactionType.Chargeback;

            Guid? reversedId = null;
            if (!string.IsNullOrEmpty(request.ReversedConversionId) && Guid.TryParse(request.ReversedConversionId, out var rId))
            {
                reversedId = rId;
            }

            var now = DateTime.UtcNow;

            // 6. DB Execution Strategy
            var strategy = dbContext.Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync(async () =>
            {
                await using var dbTransaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

                try
                {
                    // Save Conversion record
                    var conversion = new Conversion
                    {
                        ConversionId = conversionId,
                        UserId = userId,
                        PartnerId = user.PartnerId,
                        OfferId = request.OfferId,
                        EventId = request.EventId,
                        Payout = partnerUsdShare, // Partner USD balance share
                        Type = conversionType,
                        ReversedConversionId = reversedId,
                        CreatedAt = now
                    };
                    dbContext.Conversions.Add(conversion);

                    // Credit or Debit User USD Wallet Balance
                    user.Wallet.Balance += partnerUsdShare;
                    user.Wallet.UpdatedAt = now;

                    // Log Wallet Transaction in USD
                    var transaction = new WalletTransaction
                    {
                        TransactionId = Guid.CreateVersion7(),
                        UserId = userId,
                        Amount = partnerUsdShare,
                        Type = transactionType,
                        ConversionId = conversionId,
                        CreatedAt = now
                    };
                    dbContext.WalletTransactions.Add(transaction);

                    // Update Click Status if K1 (ClickId) is attached
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
                    await dbTransaction.CommitAsync(cancellationToken);

                    return Result.Success();
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
        private static readonly string[] AllowedIps =
        {
            "34.118.33.53", "138.68.125.171", "64.226.93.56",
            "74.220.53.15", "74.220.53.234", "74.220.53.235"
        };

        public void AddRoutes(IEndpointRouteBuilder app)
        {
            // Support POST (JSON Body)
            app.MapPost("/api/webhooks/klink", HandlePostback)
               .AllowAnonymous()
               .WithTags("Webhooks");

            // Support GET (Query Parameters)
            app.MapGet("/api/webhooks/klink", HandlePostback)
               .AllowAnonymous()
               .WithTags("Webhooks");
        }

        private static async Task<IResult> HandlePostback(
            HttpContext context,
            [AsParameters] Command command,
            ISender sender,
            CancellationToken cancellationToken)
        {
            var remoteIp = GetClientIp(context);

            if (string.IsNullOrEmpty(remoteIp) || !AllowedIps.Contains(remoteIp))
            {
                return Results.Unauthorized();
            }

            var result = await sender.Send(command, cancellationToken);

            return result.IsSuccess ? Results.Ok() : Results.BadRequest(result.Error);
        }

        private static string GetClientIp(HttpContext context)
        {
            var forwardedHeader = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(forwardedHeader))
            {
                return forwardedHeader.Split(',')[0].Trim();
            }

            return context.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
        }
    }
}
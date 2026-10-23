using Carter;
using MediatR;
using OfferwallApi.Infrastructure.Interfaces;
using OfferwallApi.Infrastructure.Messaging;
using OfferwallApi.Infrastructure.Persistence;
using OfferwallApi.Infrastructure.Results;
using Microsoft.EntityFrameworkCore;
using OfferwallApi.Infrastructure.Authorization;
using OfferwallApi.Shared.Entities;

namespace OfferwallApi.Features.Partner.Dashboard;

public static class PartnerOverview
{
    [Authorize(Role = "Partner")]
    public sealed record Query : IQuery<Result<Response>>;

    public sealed record Response(
        string CompanyName,
        string? LogoUrl,
        int CouponsGenerated,
        int CouponsRedeemed,
        decimal TotalPaid,
        decimal TotalInThisMonth,
        decimal PendingSettlements,
        decimal Chargebacks
    );

    internal sealed class Handler(
        ApplicationDbContext dbContext,
        ICurrentUser currentUser)
        : IQueryHandler<Query, Result<Response>>
    {
        public async Task<Result<Response>> Handle(
            Query request,
            CancellationToken cancellationToken)
        {
            var partnerId = currentUser.UserId;

            // 1. Check Partner existence and fetch HasChargebacks status
            var partner = await dbContext.Partners
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.PartnerId == partnerId,
                    cancellationToken);

            if (partner is null)
            {
                return Error.NotFound(
                    "Partner.NotFound",
                    "The partner was not found.");
            }

            // 2. Fetch Coupon and Payout statistics
            var couponsGenerated = await dbContext.Coupons
                .AsNoTracking()
                .CountAsync(x => x.PartnerId == partnerId, cancellationToken);

            var couponsRedeemed = await dbContext.Coupons
                .AsNoTracking()
                .CountAsync(
                    x => x.PartnerId == partnerId && x.Status == CouponStatus.Redeemed,
                    cancellationToken);

            var totalPaid = await dbContext.Payouts
                .AsNoTracking()
                .Where(x => x.PartnerId == partnerId && x.Status == PayoutStatus.Paid)
                .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;

            // 3. Setup Calendar Dates for Net-30
            var now = DateTime.UtcNow;
            var startOfCurrentMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var clearedThreshold = startOfCurrentMonth.AddMonths(-1);

            // 4. Single Light Query for Conversions starting from ClearedThreshold
            var conversionData = await dbContext.Conversions
                .AsNoTracking()
                .Where(c => c.PartnerId == partnerId && c.CreatedAt >= clearedThreshold)
                .Select(c => new
                {
                    c.Payout,
                    c.Type,
                    c.CreatedAt
                })
                .ToListAsync(cancellationToken);

            // A. Conversions Calculations
            var thisMonthConversions = conversionData
                .Where(c => c.CreatedAt >= startOfCurrentMonth && c.Type == ConversionType.Conversion)
                .Sum(c => c.Payout);

            var pendingConversions = conversionData
                .Where(c => c.Type == ConversionType.Conversion)
                .Sum(c => c.Payout);

            decimal thisMonthChargebacks = 0m;
            decimal pendingChargebacks = 0m;

            if (partner.HasChargebacks)
            {
                thisMonthChargebacks = conversionData
                    .Where(c => c.CreatedAt >= startOfCurrentMonth && c.Type == ConversionType.Chargeback)
                    .Sum(c => c.Payout);

                pendingChargebacks = conversionData
                    .Where(c => c.Type == ConversionType.Chargeback)
                    .Sum(c => c.Payout);
            }

            // C. Net Totals
            var totalInThisMonth = thisMonthConversions - thisMonthChargebacks;
            var pendingSettlements = pendingConversions - pendingChargebacks;

            return new Response(
                partner.CompanyName,
                partner.LogoUrl,
                couponsGenerated,
                couponsRedeemed,
                totalPaid,
                totalInThisMonth,
                pendingSettlements,
                thisMonthChargebacks);
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapGet(
                "/partners/dashboard/overview",
                async (ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(new Query(), cancellationToken);
                    return result.ToHttpResponse();
                });
        }
    }
}
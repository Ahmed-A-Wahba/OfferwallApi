using Carter;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OfferwallApi.Infrastructure.Authorization;
using OfferwallApi.Infrastructure.Messaging;
using OfferwallApi.Infrastructure.Persistence;
using OfferwallApi.Infrastructure.Results;
using OfferwallApi.Shared.Entities;

namespace OfferwallApi.Features.Admin.Dashboard;

public static class GetDashboardKpis
{
    [Authorize(Role = "Admin")]
    public sealed record Query(
        DateTime StartDate,
        DateTime EndDate
    ) : IQuery<Result<Response>>;

    public sealed record Response(
        KpiMetric RevenueToday,
        KpiMetric RevenueTotal,
        KpiMetric Profit,
        KpiMetric Clicks,
        KpiMetric CompletedOffers,
        KpiMetric Epc,
        int ActiveOffers,
        decimal ApprovedRevenue,
        decimal RejectedRevenue
    );

    public sealed record KpiMetric(decimal Value, decimal PercentageChange);

    internal sealed class Validator : AbstractValidator<Query>
    {
        public Validator()
        {
            RuleFor(x => x.StartDate).NotEmpty();
            RuleFor(x => x.EndDate).NotEmpty()
                .GreaterThanOrEqualTo(x => x.StartDate);
        }
    }

    public sealed class Handler(
        ApplicationDbContext dbContext
    ) : IQueryHandler<Query, Result<Response>>
    {
        public async Task<Result<Response>> Handle(Query request, CancellationToken cancellationToken)
        {
            var currentPeriodDays = (request.EndDate - request.StartDate).TotalDays;
            var previousPeriodStart = request.StartDate.AddDays(-currentPeriodDays);
            var todayStart = DateTime.UtcNow.Date;
            var yesterdayStart = todayStart.AddDays(-1);

            var conversionsData = await dbContext.Conversions
                .AsNoTracking()
                .Where(c => c.CreatedAt >= previousPeriodStart && c.CreatedAt <= request.EndDate)
                .Select(c => new
                {
                    c.CreatedAt,
                    c.Payout,
                    c.Type,
                    PartnerShareRatio = c.User.Partner.RevenueShareRatio
                })
                .ToListAsync(cancellationToken);

            var currentConversions = conversionsData
                .Where(c => c.CreatedAt >= request.StartDate)
                .ToList();

            var previousConversions = conversionsData
                .Where(c => c.CreatedAt < request.StartDate)
                .ToList();

            var currentClicksCount = await dbContext.Clicks
                .CountAsync(c => c.CreatedAt >= request.StartDate && c.CreatedAt <= request.EndDate, cancellationToken);

            var previousClicksCount = await dbContext.Clicks
                .CountAsync(c => c.CreatedAt >= previousPeriodStart && c.CreatedAt < request.StartDate, cancellationToken);


            var currentRevenue = currentConversions.Sum(c => c.Payout);
            var prevRevenue = previousConversions.Sum(c => c.Payout);


            var currentProfit = currentConversions.Sum(c => c.Payout * (1 - c.PartnerShareRatio));
            var prevProfit = previousConversions.Sum(c => c.Payout * (1 - c.PartnerShareRatio));


            var currentCompleted = currentConversions.Count(c => c.Type == ConversionType.Conversion);
            var prevCompleted = previousConversions.Count(c => c.Type == ConversionType.Conversion);

            var currentEpc = currentClicksCount > 0 ? currentRevenue / currentClicksCount : 0;
            var prevEpc = previousClicksCount > 0 ? prevRevenue / previousClicksCount : 0;

            var revenueToday = currentConversions
                .Where(c => c.CreatedAt >= todayStart)
                .Sum(c => c.Payout);

            var revenueYesterday = currentConversions
                .Where(c => c.CreatedAt >= yesterdayStart && c.CreatedAt < todayStart)
                .Sum(c => c.Payout);

            var activeOffers = await dbContext.OfferCaches.CountAsync(o => o.IsActive, cancellationToken);
            var approvedRevenue = currentConversions.Where(c => c.Type == ConversionType.Conversion).Sum(c => c.Payout);
            var rejectedRevenue = currentConversions.Where(c => c.Type == ConversionType.Chargeback).Sum(c => c.Payout);

            return new Response(
                RevenueToday: new KpiMetric(revenueToday, CalculatePercentage(revenueToday, revenueYesterday)),
                RevenueTotal: new KpiMetric(currentRevenue, CalculatePercentage(currentRevenue, prevRevenue)),
                Profit: new KpiMetric(currentProfit, CalculatePercentage(currentProfit, prevProfit)),
                Clicks: new KpiMetric(currentClicksCount, CalculatePercentage(currentClicksCount, previousClicksCount)),
                CompletedOffers: new KpiMetric(currentCompleted, CalculatePercentage(currentCompleted, prevCompleted)),
                Epc: new KpiMetric(currentEpc, CalculatePercentage(currentEpc, prevEpc)),
                ActiveOffers: activeOffers,
                ApprovedRevenue: approvedRevenue,
                RejectedRevenue: rejectedRevenue
            );
        }

        private static decimal CalculatePercentage(decimal current, decimal previous)
        {
            if (previous == 0) return current > 0 ? 100 : 0;
            var change = ((current - previous) / previous) * 100;
            return Math.Round(change, 2);
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapGet(
                "/admin/dashboard/kpis",
                async ([AsParameters] Query query, ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(query, cancellationToken);
                    return result.ToHttpResponse();
                });
        }
    }
}
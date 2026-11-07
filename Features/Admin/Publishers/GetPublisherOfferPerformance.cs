using Carter;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OfferwallApi.Infrastructure.Authorization;
using OfferwallApi.Infrastructure.Messaging;
using OfferwallApi.Infrastructure.Persistence;
using OfferwallApi.Infrastructure.Results;
using OfferwallApi.Shared.Entities;

namespace OfferwallApi.Features.Admin.Publishers;

public static class GetPublisherOfferPerformance
{
    [Authorize(Role = "Admin")]
    public sealed record Query(
        Guid PublisherId,
        string? SearchTerm = null,
        string? SortBy = null,
        DateTime? FromDate = null,
        DateTime? ToDate = null,
        int PageNumber = 1,
        int PageSize = 10
    ) : IQuery<Result<Response>>;

    public sealed record Response(
        PerformanceSummary Summary,
        int TotalOffersCount,
        List<OfferPerformanceItemDto> Offers
    );

    public sealed record PerformanceSummary(
        int OffersShown,
        int TotalCompletions,
        decimal TotalRevenue,
        decimal AverageEpc
    );

    public sealed record OfferPerformanceItemDto(
        int Rank,
        string OfferId,
        string OfferName,
        string OfferShortId,
        string Provider,
        int Completions,
        decimal Revenue,
        decimal Epc,
        DateTime? LastActivity,
        string Status
    );

    internal sealed class Validator : AbstractValidator<Query>
    {
        public Validator()
        {
            RuleFor(x => x.PublisherId).NotEmpty();
            RuleFor(x => x.PageNumber).GreaterThanOrEqualTo(1);
            RuleFor(x => x.PageSize).GreaterThan(0);
        }
    }

    internal sealed class Handler(ApplicationDbContext dbContext)
        : IQueryHandler<Query, Result<Response>>
    {
        public async Task<Result<Response>> Handle(Query request, CancellationToken cancellationToken)
        {
            var fromDate = request.FromDate ?? DateTime.UtcNow.AddMonths(-1);
            var toDate = request.ToDate ?? DateTime.UtcNow;

            // 1. جلب نسبة الشريك (RevenueShareRatio) مرة واحدة
            var partnerShareRatio = await dbContext.Partners
                .AsNoTracking()
                .Where(p => p.PartnerId == request.PublisherId)
                .Select(p => p.RevenueShareRatio)
                .FirstOrDefaultAsync(cancellationToken);

            // 2. سحب التحويلات الخاصة بالناشر في النطاق الزمني
            var rawConversions = await dbContext.Conversions
                .AsNoTracking()
                .Where(c => c.PartnerId == request.PublisherId
                         && c.CreatedAt >= fromDate
                         && c.CreatedAt <= toDate
                         && c.Type == ConversionType.Conversion
                         && c.OfferId != null)
                .Select(c => new
                {
                    c.OfferId,
                    OfferName = c.OfferCache != null ? c.OfferCache.Name : "Unknown Offer",
                    IsActive = c.OfferCache != null && c.OfferCache.IsActive,
                    Payout = Math.Abs(c.Payout),
                    c.CreatedAt
                })
                .ToListAsync(cancellationToken);

            // 3. التجميع الحسابي وتطبيق المعادلة المالية للمنصة (Payout + Profit)
            var offerGroups = rawConversions
                .GroupBy(c => c.OfferId!)
                .Select(g =>
                {
                    var publisherPayout = g.Sum(x => x.Payout);
                    var profit = CalculateProfit(publisherPayout, partnerShareRatio);
                    var totalRevenue = publisherPayout + profit;
                    var completions = g.Count();

                    return new
                    {
                        OfferId = g.Key,
                        OfferName = g.First().OfferName,
                        IsActive = g.First().IsActive,
                        Completions = completions,
                        PublisherPayout = publisherPayout,
                        TotalRevenue = totalRevenue,
                        Epc = completions > 0 ? Math.Round(totalRevenue / completions, 4) : 0m,
                        LastActivity = g.Max(x => (DateTime?)x.CreatedAt)
                    };
                })
                .AsQueryable();

            // 4. تطبيق التصفية (Search)
            if (!string.IsNullOrWhiteSpace(request.SearchTerm))
            {
                var term = request.SearchTerm.Trim().ToLower();
                offerGroups = offerGroups.Where(o =>
                    o.OfferName.ToLower().Contains(term) ||
                    o.OfferId.ToLower().Contains(term));
            }

            // 5. حساب الإحصائيات الإجمالية (Summary)
            var totalCompletions = offerGroups.Sum(o => o.Completions);
            var overallTotalRevenue = offerGroups.Sum(o => o.TotalRevenue);
            var overallAvgEpc = totalCompletions > 0 ? Math.Round(overallTotalRevenue / totalCompletions, 4) : 0m;
            var totalOffersCount = offerGroups.Count();

            // 6. الترتيب (Sorting)
            offerGroups = request.SortBy?.ToLower() switch
            {
                "revenue" => offerGroups.OrderByDescending(o => o.TotalRevenue),
                "completions" => offerGroups.OrderByDescending(o => o.Completions),
                _ => offerGroups.OrderByDescending(o => o.TotalRevenue)
            };

            // 7. الـ Pagination
            var pagedOffers = offerGroups
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToList();

            // 8. بناء الـ Response
            int rankStart = ((request.PageNumber - 1) * request.PageSize) + 1;
            var offerItems = pagedOffers.Select((o, index) => new OfferPerformanceItemDto(
                Rank: rankStart + index,
                OfferId: o.OfferId,
                OfferName: o.OfferName,
                OfferShortId: o.OfferId.Length >= 6 ? o.OfferId[..6].ToUpper() : o.OfferId.ToUpper(),
                Provider: "Klinklabs",
                Completions: o.Completions,
                Revenue: o.TotalRevenue,
                Epc: o.Epc,
                LastActivity: o.LastActivity,
                Status: o.IsActive ? "Active" : "Hidden"
            )).ToList();

            var summary = new PerformanceSummary(
                OffersShown: totalOffersCount,
                TotalCompletions: totalCompletions,
                TotalRevenue: overallTotalRevenue,
                AverageEpc: overallAvgEpc
            );

            return new Response(summary, totalOffersCount, offerItems);
        }

        private static decimal CalculateProfit(decimal publisherPayout, decimal shareRatio)
        {
            if (shareRatio <= 0 || shareRatio >= 1) return 0m;

            var total = publisherPayout / shareRatio;
            return Math.Round(total - publisherPayout, 4, MidpointRounding.AwayFromZero);
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapGet("/admin/publishers/{publisherId:guid}/offer-performance", async (
                [AsParameters] Query query,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(query, cancellationToken);
                return result.ToHttpResponse();
            });
        }
    }
}
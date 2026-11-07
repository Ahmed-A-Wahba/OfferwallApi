using Carter;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OfferwallApi.Infrastructure.Authorization;
using OfferwallApi.Infrastructure.Messaging;
using OfferwallApi.Infrastructure.Persistence;
using OfferwallApi.Infrastructure.Results;
using OfferwallApi.Shared.Entities;

namespace OfferwallApi.Features.Admin.Offers;

public static class GetAdminOffers
{
    // 1. Query
    [Authorize(Role = "Admin")]
    public sealed record Query(
        string? SearchTerm = null,
        string? Status = null, // "Active" or "Hidden"
        string? Source = null,
        string? Country = null,
        int PageNumber = 1,
        int PageSize = 10
    ) : IQuery<Result<Response>>;

    // 2. Response DTOs
    public sealed record Response(
        OffersSummary Summary,
        int TotalCount,
        List<OfferItemDto> Offers
    );

    public sealed record OffersSummary(
        int TotalOffers,
        int AddedToday,
        int Active,
        int Hidden
    );

    public sealed record OfferItemDto(
        string OfferId,
        string ShortId,
        string Name,
        string LogoUrl,
        string Source,
        decimal Payout,
        decimal Epc,
        int PublishersCount,
        string Status
    );

    // 3. Validator
    internal sealed class Validator : AbstractValidator<Query>
    {
        public Validator()
        {
            RuleFor(x => x.PageNumber).GreaterThanOrEqualTo(1);
            RuleFor(x => x.PageSize).GreaterThan(0);
        }
    }

    // 4. Handler
    internal sealed class Handler(ApplicationDbContext dbContext)
        : IQueryHandler<Query, Result<Response>>
    {
        public async Task<Result<Response>> Handle(Query request, CancellationToken cancellationToken)
        {
            var query = dbContext.OfferCaches.AsNoTracking();

            var today = DateTime.UtcNow.Date;

            // 1. حساب الإحصائيات العامة (Summary Stats)
            var summaryStats = await query
                .Select(o => new
                {
                    IsToday = o.CreatedAt >= today,
                    o.IsActive
                })
                .GroupBy(x => 1)
                .Select(g => new OffersSummary(
                    g.Count(),
                    g.Count(o => o.IsToday),
                    g.Count(o => o.IsActive),
                    g.Count(o => !o.IsActive)
                ))
                .FirstOrDefaultAsync(cancellationToken)
                ?? new OffersSummary(0, 0, 0, 0);

            // 2. تطبيق الفلاتر
            if (!string.IsNullOrWhiteSpace(request.SearchTerm))
            {
                var term = request.SearchTerm.Trim();
                query = query.Where(o =>
                    EF.Functions.Like(o.Name, $"%{term}%") ||
                    EF.Functions.Like(o.OfferId, $"%{term}%"));
            }

            if (!string.IsNullOrWhiteSpace(request.Status) && !request.Status.Equals("All Status", StringComparison.OrdinalIgnoreCase))
            {
                bool isActive = request.Status.Equals("Active", StringComparison.OrdinalIgnoreCase);
                query = query.Where(o => o.IsActive == isActive);
            }

            int totalCount = await query.CountAsync(cancellationToken);

            // 3. تطبيق الـ Pagination
            var pagedOffers = await query
                .OrderByDescending(o => o.CreatedAt)
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync(cancellationToken);

            var offerIds = pagedOffers.Select(o => o.OfferId).ToList();

            // 4. سحب تحويلات هذه العروض وحساب الـ Total Revenue والـ EPC بالمعيار المالي الموحد
            var rawConversions = await dbContext.Conversions
                .AsNoTracking()
                .Where(c => offerIds.Contains(c.OfferId) && c.Type == ConversionType.Conversion)
                .Select(c => new
                {
                    c.OfferId,
                    c.PartnerId,
                    Payout = Math.Abs(c.Payout),
                    PartnerShareRatio = c.User != null && c.User.Partner != null
                        ? c.User.Partner.RevenueShareRatio
                        : 1.0m
                })
                .ToListAsync(cancellationToken);

            var statsLookup = rawConversions
                .GroupBy(c => c.OfferId)
                .ToDictionary(
                    g => g.Key,
                    g =>
                    {
                        var completions = g.Count();
                        var totalRevenue = g.Sum(x => x.Payout + CalculateProfit(x.Payout, x.PartnerShareRatio));
                        var publishersCount = g.Select(x => x.PartnerId).Distinct().Count();

                        return new
                        {
                            Completions = completions,
                            TotalRevenue = totalRevenue,
                            Epc = completions > 0 ? Math.Round(totalRevenue / completions, 4) : 0m,
                            PublishersCount = publishersCount
                        };
                    }
                );

            // 5. تحويل البيانات إلى DTOs
            var offerItems = pagedOffers.Select(o =>
            {
                var stats = statsLookup.GetValueOrDefault(o.OfferId);

                return new OfferItemDto(
                    OfferId: o.OfferId,
                    ShortId: o.OfferId.Length >= 6 ? o.OfferId[..6].ToUpper() : o.OfferId.ToUpper(),
                    Name: o.Name,
                    LogoUrl: o.LogoUrl,
                    Source: "Klinklabs",
                    Payout: o.TotalReward,
                    Epc: stats?.Epc ?? 0m,
                    PublishersCount: stats?.PublishersCount ?? 0,
                    Status: o.IsActive ? "ACTIVE" : "HIDDEN"
                );
            }).ToList();

            return new Response(summaryStats, totalCount, offerItems);
        }

        private static decimal CalculateProfit(decimal publisherPayout, decimal shareRatio)
        {
            if (shareRatio <= 0 || shareRatio >= 1) return 0m;

            var total = publisherPayout / shareRatio;
            return Math.Round(total - publisherPayout, 4, MidpointRounding.AwayFromZero);
        }
    }

    // 5. Endpoint
    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapGet("/admin/offers", async (
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
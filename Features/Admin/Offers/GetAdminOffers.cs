using Carter;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc;
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
    [Authorize(Role = "Admin")]
    internal sealed class Handler(ApplicationDbContext dbContext)
        : IQueryHandler<Query, Result<Response>>
    {
        public async Task<Result<Response>> Handle(Query request, CancellationToken cancellationToken)
        {
            var query = dbContext.OfferCaches.AsNoTracking().AsQueryable();

            // Calculate Summary Statistics (Before applying pagination/search filters)
            var today = DateTime.UtcNow.Date;

            var summaryStats = await query
                .GroupBy(x => 1)
                .Select(g => new OffersSummary(
                    g.Count(),
                    g.Count(o => o.CreatedAt >= today),
                    g.Count(o => o.IsActive),
                    g.Count(o => !o.IsActive)
                ))
                .FirstOrDefaultAsync(cancellationToken)
                ?? new OffersSummary(0, 0, 0, 0);

            // Apply Filters
            if (!string.IsNullOrWhiteSpace(request.SearchTerm))
            {
                var searchTerm = request.SearchTerm.ToLower();
                query = query.Where(o =>
                    o.Name.ToLower().Contains(searchTerm) ||
                    o.OfferId.ToLower().Contains(searchTerm));
            }

            if (!string.IsNullOrWhiteSpace(request.Status) && request.Status != "All Status")
            {
                bool isActive = request.Status.Equals("Active", StringComparison.OrdinalIgnoreCase);
                query = query.Where(o => o.IsActive == isActive);
            }

            // Note: If you add Provider/Source or Countries to OfferCache later, you can filter them here:
            // if (!string.IsNullOrWhiteSpace(request.Source) && request.Source != "All Sources") ...

            // Get total count for pagination
            int totalCount = await query.CountAsync(cancellationToken);

            // Apply Pagination
            var pagedOffers = await query
                .OrderByDescending(o => o.CreatedAt)
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync(cancellationToken);

            // Fetch conversions stats for the current page offers to calculate EPC & Publishers
            var offerIds = pagedOffers.Select(o => o.OfferId).ToList();

            var statsQuery = await dbContext.Conversions
                .AsNoTracking()
                .Where(c => offerIds.Contains(c.OfferId) && c.Type == ConversionType.Conversion)
                .GroupBy(c => c.OfferId)
                .Select(g => new
                {
                    OfferId = g.Key,
                    Completions = g.Count(),
                    Revenue = g.Sum(x => x.Payout),
                    PublishersCount = g.Select(x => x.PartnerId).Distinct().Count()
                })
                .ToDictionaryAsync(x => x.OfferId, cancellationToken);

            // Map to DTO
            var offerItems = pagedOffers.Select(o =>
            {
                var stats = statsQuery.GetValueOrDefault(o.OfferId);
                var completions = stats?.Completions ?? 0;
                var revenue = stats?.Revenue ?? 0m;
                var epc = completions > 0 ? revenue / completions : 0m;

                return new OfferItemDto(
                    OfferId: o.OfferId,
                    ShortId: o.OfferId.Length >= 6 ? o.OfferId.Substring(0, 6).ToUpper() : o.OfferId.ToUpper(),
                    Name: o.Name,
                    LogoUrl: o.LogoUrl,
                    Source: "Klinklabs", // Adjust this if you add 'Provider' to OfferCache
                    Payout: o.TotalReward,
                    Epc: Math.Round(epc, 2),
                    PublishersCount: stats?.PublishersCount ?? 0,
                    Status: o.IsActive ? "ACTIVE" : "HIDDEN"
                );
            }).ToList();

            return new Response(summaryStats, totalCount, offerItems);
        }
    }

    // 5. Endpoint
    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapGet("/admin/offers", async (
                [FromQuery] string? searchTerm,
                [FromQuery] string? status,
                [FromQuery] string? source,
                [FromQuery] string? country,
                [FromQuery] int pageNumber,
                [FromQuery] int pageSize,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                pageNumber = pageNumber > 0 ? pageNumber : 1;
                pageSize = pageSize > 0 ? pageSize : 10;

                var query = new Query(searchTerm, status, source, country, pageNumber, pageSize);
                var result = await sender.Send(query, cancellationToken);

                return result.ToHttpResponse();
            })
            .RequireAuthorization();
        }
    }
}
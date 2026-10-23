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

namespace OfferwallApi.Features.Admin.Publishers;

public static class GetPublisherOfferPerformance
{
    // 1. Query
    public sealed record Query(
        Guid PublisherId,
        string? SearchTerm = null,
        string? SortBy = null,
        DateTime? FromDate = null,
        DateTime? ToDate = null,
        int PageNumber = 1,
        int PageSize = 10
    ) : IQuery<Result<Response>>;

    // 2. Response DTOs
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

    // 3. Validator
    internal sealed class Validator : AbstractValidator<Query>
    {
        public Validator()
        {
            RuleFor(x => x.PublisherId).NotEmpty();
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
            var fromDate = request.FromDate ?? DateTime.UtcNow.AddMonths(-1);
            var toDate = request.ToDate ?? DateTime.UtcNow;

            // Base query for conversions related to this publisher in the date range
            var conversionsQuery = dbContext.Conversions
                .AsNoTracking()
                .Include(c => c.OfferCache)
                .Where(c => c.PartnerId == request.PublisherId
                         && c.CreatedAt >= fromDate
                         && c.CreatedAt <= toDate
                         && c.Type == ConversionType.Conversion);

            // Calculate overall summary stats
            int totalCompletions = await conversionsQuery.CountAsync(cancellationToken);
            decimal totalRevenue = await conversionsQuery.SumAsync(c => (decimal?)c.Payout, cancellationToken) ?? 0m;
            decimal avgEpc = totalCompletions > 0 ? totalRevenue / totalCompletions : 0m;

            // Group by Offer to build the list
            var offerGroupQuery = conversionsQuery
                .Where(c => c.OfferCache != null)
                .GroupBy(c => c.OfferCache)
                .Select(g => new
                {
                    Offer = g.Key,
                    Completions = g.Count(),
                    Revenue = g.Sum(x => x.Payout),
                    LastActivity = g.Max(x => (DateTime?)x.CreatedAt)
                });

            // Apply Filters (Search)
            if (!string.IsNullOrWhiteSpace(request.SearchTerm))
            {
                string searchTerm = request.SearchTerm.ToLower();
                offerGroupQuery = offerGroupQuery.Where(o =>
                    o.Offer!.Name.ToLower().Contains(searchTerm) ||
                    o.Offer.OfferId.ToLower().Contains(searchTerm));
            }

            // Apply Sorting
            offerGroupQuery = request.SortBy?.ToLower() switch
            {
                "revenue" => offerGroupQuery.OrderByDescending(o => o.Revenue),
                "completions" => offerGroupQuery.OrderByDescending(o => o.Completions),
                _ => offerGroupQuery.OrderByDescending(o => o.Revenue)
            };

            // Get total count for pagination
            int totalOffersCount = await offerGroupQuery.CountAsync(cancellationToken);

            // Apply Pagination
            var pagedOffers = await offerGroupQuery
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync(cancellationToken);

            // Map to DTO
            int rankStart = ((request.PageNumber - 1) * request.PageSize) + 1;
            var offerItems = pagedOffers.Select((o, index) => new OfferPerformanceItemDto(
                Rank: rankStart + index,
                OfferId: o.Offer!.OfferId,
                OfferName: o.Offer.Name,
                OfferShortId: o.Offer.OfferId.Length >= 6 ? o.Offer.OfferId.Substring(0, 6).ToUpper() : o.Offer.OfferId.ToUpper(),
                Provider: "Klinklabs", // Assuming provider is Klinklabs; adjust as necessary
                Completions: o.Completions,
                Revenue: o.Revenue,
                Epc: o.Completions > 0 ? Math.Round(o.Revenue / o.Completions, 2) : 0m,
                LastActivity: o.LastActivity,
                Status: o.Offer.IsActive ? "Active" : "Hidden"
            )).ToList();

            var summary = new PerformanceSummary(
                OffersShown: totalOffersCount,
                TotalCompletions: totalCompletions,
                TotalRevenue: totalRevenue,
                AverageEpc: Math.Round(avgEpc, 2)
            );

            return new Response(summary, totalOffersCount, offerItems);
        }
    }

    // 5. Endpoint
    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapGet("/admin/publishers/{id:guid}/offer-performance", async (
                Guid id,
                [FromQuery] string? searchTerm,
                [FromQuery] string? sortBy,
                [FromQuery] DateTime? fromDate,
                [FromQuery] DateTime? toDate,
                [FromQuery] int pageNumber,
                [FromQuery] int pageSize,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                // Default pagination values if not provided
                pageNumber = pageNumber > 0 ? pageNumber : 1;
                pageSize = pageSize > 0 ? pageSize : 10;

                var query = new Query(id, searchTerm, sortBy, fromDate, toDate, pageNumber, pageSize);
                var result = await sender.Send(query, cancellationToken);
                return result.ToHttpResponse();
            })
            .RequireAuthorization();
        }
    }
}
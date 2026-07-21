using Carter;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OfferwallApi.Infrastructure.Authorization;
using OfferwallApi.Infrastructure.Interfaces;
using OfferwallApi.Infrastructure.Messaging;
using OfferwallApi.Infrastructure.Persistence;
using OfferwallApi.Infrastructure.Results;
using OfferwallApi.Shared.Entities;

namespace OfferwallApi.Features.Partner.Dashboard;

public static class PartnerPerformanceByOffer
{
    public sealed record Query(
        DateOnly From,
        DateOnly To,
        OfferPerformanceSortBy SortBy = OfferPerformanceSortBy.Revenue,
        SortDirection SortDirection = SortDirection.Descending
    ) : IQuery<Result<Response>>;

    public enum OfferPerformanceSortBy
    {
        Revenue,
        Generated,
        Redeemed,
        ConversionRate,
        OfferName
    }

    public enum SortDirection
    {
        Ascending,
        Descending
    }

    public sealed record Response(
        IReadOnlyList<OfferPerformanceItem> Offers
    );

    public sealed record OfferPerformanceItem(
        string OfferId,
        string OfferName,
        string OfferLogoUrl,
        int Clicks,
        int Conversions,
        decimal Earnings
    );

    internal sealed class Validator : AbstractValidator<Query>
    {
        public Validator()
        {
            RuleFor(x => x.From)
                .LessThanOrEqualTo(x => x.To);

            RuleFor(x => x.To)
                .LessThanOrEqualTo(DateOnly.FromDateTime(DateTime.UtcNow));
        }
    }

    [Authorize(Role = "Partner")]
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

            var from = request.From.ToDateTime(TimeOnly.MinValue);
            var to = request.To.ToDateTime(TimeOnly.MaxValue);

            var clicks = await dbContext.Clicks
                .Where(x =>
                    x.User.PartnerId == partnerId &&
                    x.CreatedAt >= from &&
                    x.CreatedAt <= to)
                .GroupBy(x => x.OfferId)
                .Select(x => new
                {
                    x.Key,
                    Count = x.Count()
                })
                .ToDictionaryAsync(
                    x => x.Key,
                    x => x.Count,
                    cancellationToken);

            var conversions = await dbContext.Conversions
                .Where(x =>
                    x.User.PartnerId == partnerId &&
                    x.Type == ConversionType.Conversion &&
                    x.CreatedAt >= from &&
                    x.CreatedAt <= to)
                .GroupBy(x => x.OfferId)
                .Select(x => new
                {
                    x.Key,
                    Count = x.Count(),
                    Earnings = x.Sum(c => c.Payout)
                })
                .ToDictionaryAsync(
                    x => x.Key,
                    cancellationToken);

            var offerIds = clicks.Keys
                .Union(conversions.Keys)
                .ToList();

            var offers = await dbContext.OfferCaches
                .Where(x => offerIds.Contains(x.OfferId))
                .ToListAsync(cancellationToken);

            var response = offers
                .Select(x =>
                {
                    clicks.TryGetValue(x.OfferId, out var clickCount);

                    conversions.TryGetValue(x.OfferId, out var conversion);

                    return new OfferPerformanceItem(
                        x.OfferId,
                        x.Name,
                        x.LogoUrl,
                        clickCount,
                        conversion?.Count ?? 0,
                        conversion?.Earnings ?? 0);
                })
                .ToList();

            return new Response(response);
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapGet(
                "/partners/dashboard/performance-by-offer",
                async (
                    Query query,
                    ISender sender) =>
                {
                    var result = await sender.Send(query);

                    return result.ToHttpResponse();
                })
                .RequireAuthorization();
        }
    }
}
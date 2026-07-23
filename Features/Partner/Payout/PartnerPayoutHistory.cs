using Carter;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OfferwallApi.Infrastructure.Authorization;
using OfferwallApi.Infrastructure.Interfaces;
using OfferwallApi.Infrastructure.Messaging;
using OfferwallApi.Infrastructure.Persistence;
using OfferwallApi.Shared.Entities;

namespace OfferwallApi.Features.Partner.Payout;

public static class PartnerPayoutHistory
{
    [Authorize(Role = "Partner")]
    public sealed record Query(
        int Page = 1,
        int PageSize = 10
    ) : IQuery<Response>;

    public sealed record Response(
        IReadOnlyList<PayoutItem> Items,
        int TotalCount
    );

    public sealed record PayoutItem(
        DateOnly PeriodStart,
        DateOnly PeriodEnd,
        DateOnly? PaidAt,
        int RedemptionCount,
        decimal Amount,
        PayoutStatus Status
    );

    internal sealed class Validator : AbstractValidator<Query>
    {
        public Validator()
        {
            RuleFor(x => x.Page)
                .GreaterThan(0);

            RuleFor(x => x.PageSize)
                .InclusiveBetween(1, 100);
        }
    }

    internal sealed class Handler(
    ApplicationDbContext context,
    ICurrentUser currentUser)
    : IQueryHandler<Query, Response>
    {
        public async Task<Response> Handle(
            Query request,
            CancellationToken cancellationToken)
        {
            var partnerId = currentUser.UserId;

            var query = context.Payouts
                .AsNoTracking()
                .Where(x => x.PartnerId == partnerId)
                .OrderByDescending(x => x.PeriodStart);

            var totalCount = await query.CountAsync(cancellationToken);

            var items = await query
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(x => new PayoutItem(
                    DateOnly.FromDateTime(x.PeriodStart),
                    DateOnly.FromDateTime(x.PeriodEnd),
                    x.PaidAt.HasValue
                        ? DateOnly.FromDateTime(x.PaidAt.Value)
                        : null,
                    x.RedemptionCount,
                    x.Amount,
                    x.Status))
                .ToListAsync(cancellationToken);

            var currentPeriodStart = new DateTime(
                DateTime.UtcNow.Year,
                DateTime.UtcNow.Month,
                1);

            var currentPeriodEnd = currentPeriodStart
                .AddMonths(1)
                .AddDays(-1);

            var currentPeriod = DateOnly.FromDateTime(currentPeriodStart);

            var hasCurrentPayout = await context.Payouts
                .AnyAsync(
                    x => x.PartnerId == partnerId &&
                         x.PeriodStart == currentPeriodStart,
                    cancellationToken);

            if (!hasCurrentPayout)
            {
                var current = await context.Coupons
                    .AsNoTracking()
                    .Where(x =>
                        x.PartnerId == partnerId &&
                        x.CreatedAt >= currentPeriodStart &&
                        x.CreatedAt <= currentPeriodEnd)
                    .GroupBy(_ => 1)
                    .Select(g => new
                    {
                        RedemptionCount = g.Count(),
                        Amount = g.Sum(x => x.Value)
                    })
                    .FirstOrDefaultAsync(cancellationToken);

                items.Insert(0, new PayoutItem(
                    currentPeriod,
                    DateOnly.FromDateTime(currentPeriodEnd),
                    null,
                    current?.RedemptionCount ?? 0,
                    current?.Amount ?? 0,
                    PayoutStatus.Pending));

                totalCount++;
            }

            return new Response(
                items,
                totalCount);
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapGet("/partner/payouts/history",
                async (
                    [AsParameters] Query query,
                    ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    var response = await sender.Send(
                        query,
                        cancellationToken);

                    return Results.Ok(response);
                });
        }
    }
}
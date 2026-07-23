using Carter;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OfferwallApi.Infrastructure.Authorization;
using OfferwallApi.Infrastructure.Interfaces;
using OfferwallApi.Infrastructure.Messaging;
using OfferwallApi.Infrastructure.Persistence;

namespace OfferwallApi.Features.Partner.Reports;

public static class PartnerRedemption
{
    [Authorize(Role = "Partner")]
    public sealed record Query(
        DateOnly? From,
        DateOnly? To,
        RedemptionSortBy SortBy = RedemptionSortBy.Date,
        SortDirection Direction = SortDirection.Desc,
        int Page = 1,
        int PageSize = 10
    ) : IQuery<Response>;

    public sealed record Response(
        IReadOnlyList<RedemptionItem> Items,
        int TotalCount
    );

    public sealed record RedemptionItem(
        DateTime CreatedAt,
        string UserId,
        string CouponCode,
        decimal Value,
        int Points
    );

    public enum RedemptionSortBy
    {
        Date,
        Value,
    }

    public enum SortDirection
    {
        Asc,
        Desc
    }

    internal sealed class Validator : AbstractValidator<Query>
    {
        public Validator()
        {
            RuleFor(x => x.Page)
                .GreaterThan(0);

            RuleFor(x => x.PageSize)
                .InclusiveBetween(1, 100);

            RuleFor(x => x)
                .Must(x =>
                    !x.From.HasValue ||
                    !x.To.HasValue ||
                    x.From <= x.To)
                .WithMessage("'From' date must be less than or equal to 'To' date.");
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

            var query = context.Coupons
                .AsNoTracking()
                .Where(x => x.PartnerId == partnerId);

            if (request.From.HasValue)
            {
                var from = request.From.Value.ToDateTime(TimeOnly.MinValue);

                query = query.Where(x => x.CreatedAt >= from);
            }

            if (request.To.HasValue)
            {
                var to = request.To.Value.ToDateTime(TimeOnly.MaxValue);

                query = query.Where(x => x.CreatedAt <= to);
            }

            query = request.SortBy switch
            {
                RedemptionSortBy.Value =>
                    request.Direction == SortDirection.Asc
                        ? query.OrderBy(x => x.Value)
                        : query.OrderByDescending(x => x.Value),

                _ =>
                    request.Direction == SortDirection.Asc
                        ? query.OrderBy(x => x.CreatedAt)
                        : query.OrderByDescending(x => x.CreatedAt)
            };

            var totalCount = await query.CountAsync(cancellationToken);

            var items = await query
                .Select(x => new RedemptionItem(
                    x.CreatedAt,
                    x.UserId.ToString(),
                    x.Code,
                    x.Value,
                    (int)(x.Value * x.Partner.PointsPerUsd)))
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync(cancellationToken);

            return new Response(
                items,
                totalCount);
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapGet(
                "/partners/reports/redemptions",
                async (
                    [AsParameters] Query query,
                    ISender sender) =>
                {
                    var result = await sender.Send(query);

                    return Results.Ok(result);
                });
        }
    }
}
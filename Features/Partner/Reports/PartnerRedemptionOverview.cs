using Carter;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OfferwallApi.Infrastructure.Authorization;
using OfferwallApi.Infrastructure.Interfaces;
using OfferwallApi.Infrastructure.Messaging;
using OfferwallApi.Infrastructure.Persistence;

namespace OfferwallApi.Features.Partner.Reports;

public static class PartnerRedemptionOverview
{
    [Authorize(Role = "Partner")]
    public sealed record Query(
        DateOnly? From,
        DateOnly? To)
        : IQuery<Response>;

    public sealed record Response(
        decimal TotalRedeemedUsd,
        int TotalCouponsGenerated);

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
                .Where(c => c.PartnerId == partnerId)
                .AsQueryable();

            if (request.From is not null)
            {
                var from = request.From.Value.ToDateTime(TimeOnly.MinValue);

                query = query.Where(c => c.CreatedAt >= from);
            }

            if (request.To is not null)
            {
                var to = request.To.Value.ToDateTime(TimeOnly.MaxValue);

                query = query.Where(c => c.CreatedAt <= to);
            }

            var overview = await query
                .GroupBy(_ => 1)
                .Select(g => new Response(
                    g.Sum(x => x.Value),
                    g.Count()))
                .FirstOrDefaultAsync(cancellationToken);

            return overview ?? new Response(0m, 0);
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapGet(
                "/partners/reports/overview",
                async (
                    [AsParameters] Query query,
                    ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(
                        query,
                        cancellationToken);

                    return Results.Ok(result);
                });
        }
    }
}


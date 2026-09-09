using Carter;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OfferwallApi.Infrastructure.Interfaces;
using OfferwallApi.Infrastructure.Messaging;
using OfferwallApi.Infrastructure.Persistence;
using OfferwallApi.Infrastructure.Results;

namespace OfferwallApi.Features.Admin.Dashboard;

public static class GetDashboardEvents
{
    public sealed record Query(
        int Limit = 10
    ) : IQuery<Result<List<EventResponse>>>;

    public sealed record EventResponse(
        DateTime Time,
        string EventType,
        string Offer,
        string Publisher,
        decimal Payout,
        string Status
    );

    internal sealed class Validator : AbstractValidator<Query>
    {
        public Validator()
        {
            RuleFor(x => x.Limit).GreaterThan(0).LessThanOrEqualTo(50);
        }
    }

    public sealed class Handler(
        ApplicationDbContext dbContext
    ) : IQueryHandler<Query, Result<List<EventResponse>>>
    {
        public async Task<Result<List<EventResponse>>> Handle(Query request, CancellationToken cancellationToken)
        {
            var recentConversions = await dbContext.Conversions
                .AsNoTracking()
                .Include(c => c.OfferCache)
                .Include(c => c.User).ThenInclude(u => u.Partner)
                .OrderByDescending(c => c.CreatedAt)
                .Take(request.Limit)
                .Select(c => new EventResponse(
                    c.CreatedAt,
                    "CONVERSION",
                    c.OfferCache.Name,
                    c.User.Partner.CompanyName,
                    c.Payout,
                    c.Type.ToString().ToUpper()
                ))
                .ToListAsync(cancellationToken);

            var recentClicks = await dbContext.Clicks
                .AsNoTracking()
                .Include(c => c.OfferCache)
                .Include(c => c.User).ThenInclude(u => u.Partner)
                .OrderByDescending(c => c.CreatedAt)
                .Take(request.Limit)
                .Select(c => new EventResponse(
                    c.CreatedAt,
                    "CLICK",
                    c.OfferCache.Name,
                    c.User.Partner.CompanyName,
                    0.00m,
                    "PENDING"
                ))
                .ToListAsync(cancellationToken);

            var events = recentConversions
                .Concat(recentClicks)
                .OrderByDescending(e => e.Time)
                .Take(request.Limit)
                .ToList();

            return events;
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapGet(
                "/admin/dashboard/events",
                async ([AsParameters] Query query, ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(query, cancellationToken);
                    return result.ToHttpResponse();
                });
        }
    }
}
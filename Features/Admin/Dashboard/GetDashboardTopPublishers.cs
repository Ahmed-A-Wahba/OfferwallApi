using Carter;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OfferwallApi.Infrastructure.Authorization;
using OfferwallApi.Infrastructure.Messaging;
using OfferwallApi.Infrastructure.Persistence;
using OfferwallApi.Infrastructure.Results;

namespace OfferwallApi.Features.Admin.Dashboard;

public static class GetDashboardTopPublishers
{
    [Authorize(Role = "Admin")]
    public sealed record Query(
        DateTime StartDate,
        DateTime EndDate,
        int Limit = 4
    ) : IQuery<Result<List<PublisherStat>>>;

    public sealed record PublisherStat(
        string Name,
        int ConversionsCount,
        decimal TotalRevenue
    );

    internal sealed class Validator : AbstractValidator<Query>
    {
        public Validator()
        {
            RuleFor(x => x.StartDate).NotEmpty();
            RuleFor(x => x.EndDate).NotEmpty()
                .GreaterThanOrEqualTo(x => x.StartDate);
            RuleFor(x => x.Limit).GreaterThan(0);
        }
    }

    public sealed class Handler(
        ApplicationDbContext dbContext
    ) : IQueryHandler<Query, Result<List<PublisherStat>>>
    {
        public async Task<Result<List<PublisherStat>>> Handle(Query request, CancellationToken cancellationToken)
        {
            var rawData = await dbContext.Conversions
                .AsNoTracking()
                .Where(c => c.CreatedAt >= request.StartDate.Date && c.CreatedAt <= request.EndDate.Date.AddDays(1).AddTicks(-1))
                .Select(c => new
                {
                    CompanyName = c.User != null && c.User.Partner != null ? c.User.Partner.CompanyName : "Unknown Publisher",
                    PartnerId = c.User != null ? c.User.PartnerId : (Guid?)null,
                    Payout = Math.Abs(c.Payout),
                    PartnerShareRatio = c.User != null && c.User.Partner != null
                        ? c.User.Partner.RevenueShareRatio
                        : 1.0m
                })
                .Where(x => x.PartnerId != null)
                .ToListAsync(cancellationToken);

            var topPublishers = rawData
                .GroupBy(x => new { x.PartnerId, x.CompanyName })
                .Select(g => new PublisherStat(
                    Name: g.Key.CompanyName,
                    ConversionsCount: g.Count(),
                    TotalRevenue: g.Sum(x => x.Payout + CalculateProfit(x.Payout, x.PartnerShareRatio))
                ))
                .OrderByDescending(x => x.TotalRevenue)
                .Take(request.Limit)
                .ToList();

            return topPublishers;
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
            app.MapGet(
                "/admin/dashboard/top-publishers",
                async ([AsParameters] Query query, ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(query, cancellationToken);
                    return result.ToHttpResponse();
                });
        }
    }
}
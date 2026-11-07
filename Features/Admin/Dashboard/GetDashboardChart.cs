using Carter;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OfferwallApi.Infrastructure.Authorization;
using OfferwallApi.Infrastructure.Messaging;
using OfferwallApi.Infrastructure.Persistence;
using OfferwallApi.Infrastructure.Results;

namespace OfferwallApi.Features.Admin.Dashboard;

public static class GetDashboardChart
{
    [Authorize(Role = "Admin")]
    public sealed record Query(
        DateTime StartDate,
        DateTime EndDate
    ) : IQuery<Result<List<ChartPoint>>>;

    public sealed record ChartPoint(string Label, decimal Value);

    internal sealed class Validator : AbstractValidator<Query>
    {
        public Validator()
        {
            RuleFor(x => x.StartDate).NotEmpty();
            RuleFor(x => x.EndDate).NotEmpty()
                .GreaterThanOrEqualTo(x => x.StartDate);
        }
    }

    public sealed class Handler(
        ApplicationDbContext dbContext
    ) : IQueryHandler<Query, Result<List<ChartPoint>>>
    {
        public async Task<Result<List<ChartPoint>>> Handle(Query request, CancellationToken cancellationToken)
        {

            var conversionsData = await dbContext.Conversions
                .AsNoTracking()
                .Where(c => c.CreatedAt >= request.StartDate.Date && c.CreatedAt <= request.EndDate.Date.AddDays(1).AddTicks(-1))
                .Select(c => new
                {
                    Date = c.CreatedAt.Date,
                    Payout = Math.Abs(c.Payout),
                    PartnerShareRatio = c.User != null && c.User.Partner != null
                        ? c.User.Partner.RevenueShareRatio
                        : 1.0m
                })
                .ToListAsync(cancellationToken);

            var revenueByDate = conversionsData
                .GroupBy(c => c.Date)
                .ToDictionary(
                    g => g.Key,
                    g => g.Sum(c => c.Payout + CalculateProfit(c.Payout, c.PartnerShareRatio))
                );

            var response = new List<ChartPoint>();
            for (var date = request.StartDate.Date; date <= request.EndDate.Date; date = date.AddDays(1))
            {
                var totalRevenue = revenueByDate.TryGetValue(date, out var value) ? value : 0m;

                response.Add(new ChartPoint(
                    Label: date.ToString("MMM dd"),
                    Value: totalRevenue
                ));
            }

            return response;
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
                "/admin/dashboard/chart",
                async ([AsParameters] Query query, ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(query, cancellationToken);
                    return result.ToHttpResponse();
                });
        }
    }
}
using Carter;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OfferwallApi.Infrastructure.Interfaces;
using OfferwallApi.Infrastructure.Messaging;
using OfferwallApi.Infrastructure.Persistence;
using OfferwallApi.Infrastructure.Results;

namespace OfferwallApi.Features.Admin.Dashboard;

public static class GetDashboardChart
{
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
            var chartData = await dbContext.Conversions
                .AsNoTracking()
                .Where(c => c.CreatedAt >= request.StartDate && c.CreatedAt <= request.EndDate)
                .GroupBy(c => c.CreatedAt.Date)
                .Select(g => new
                {
                    Date = g.Key,
                    TotalRevenue = g.Sum(c => c.Payout)
                })
                .OrderBy(x => x.Date)
                .ToListAsync(cancellationToken);

            var response = chartData.Select(x => new ChartPoint(
                Label: x.Date.ToString("MMM dd"),
                Value: x.TotalRevenue
            )).ToList();

            return response;
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
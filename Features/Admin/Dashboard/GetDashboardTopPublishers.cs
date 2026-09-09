using Carter;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OfferwallApi.Infrastructure.Messaging;
using OfferwallApi.Infrastructure.Persistence;
using OfferwallApi.Infrastructure.Results;

namespace OfferwallApi.Features.Admin.Dashboard;

public static class GetDashboardTopPublishers
{
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

            var query = from c in dbContext.Conversions
                        where c.CreatedAt >= request.StartDate && c.CreatedAt <= request.EndDate
                        join u in dbContext.Users on c.UserId equals u.UserId
                        join p in dbContext.Partners on u.PartnerId equals p.PartnerId
                        group c by new { p.PartnerId, p.CompanyName } into g
                        orderby g.Sum(x => x.Payout) descending
                        select new PublisherStat(
                            g.Key.CompanyName,
                            g.Count(),
                            g.Sum(x => x.Payout)
                        );

            var topPublishers = await query.Take(request.Limit).ToListAsync(cancellationToken);

            return topPublishers;
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
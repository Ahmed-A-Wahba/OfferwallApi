using Carter;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfferwallApi.Infrastructure.Authorization;
using OfferwallApi.Infrastructure.Interfaces;
using OfferwallApi.Infrastructure.Persistence;
using OfferwallApi.Infrastructure.Results;
using OfferwallApi.Shared.Entities;

namespace OfferwallApi.Features.User.Offers;

public static class GetMyOffers
{
    [Authorize(Role = "User")]
    public sealed record Query(int Days = 7) : IRequest<Result<Response>>;

    public sealed record OfferProgressDto(
        Guid ClickId,
        string OfferId,
        string Name,
        string LogoUrl,
        decimal PointsReward,
        int CompletedTasks,
        int TotalTasks,
        int ProgressPercentage,
        string Status,
        DateTime CreatedAt
    );

    public sealed record Response(
        List<OfferProgressDto> InProgress,
        List<OfferProgressDto> Completed
    );

    public sealed class Handler(ApplicationDbContext dbContext, ICurrentUser currentUser) : IRequestHandler<Query, Result<Response>>
    {
        public async Task<Result<Response>> Handle(Query request, CancellationToken cancellationToken)
        {
            var userId = currentUser.UserId;
            var fromDate = DateTime.UtcNow.AddDays(-request.Days);

            var rawClicks = await dbContext.Clicks
                .AsNoTracking()
                .Where(c => c.UserId == userId && c.CreatedAt >= fromDate)
                .Join(
                    dbContext.OfferCaches.AsNoTracking(),
                    click => click.OfferId,
                    offer => offer.OfferId,
                    (click, offer) => new
                    {
                        click.ClickId,
                        click.OfferId,
                        click.Status,
                        click.CreatedAt,
                        offer.Name,
                        offer.LogoUrl,
                        offer.TotalReward,
                        offer.TotalTasks
                    }
                )
                .ToListAsync(cancellationToken);

            var distinctOffers = rawClicks
                .GroupBy(x => x.OfferId)
                .Select(g =>
                {
                    var completedClick = g.FirstOrDefault(c => c.Status == ClickStatus.Completed);

                    return completedClick ?? g.OrderByDescending(c => c.CreatedAt).First();
                })
                .OrderByDescending(x => x.CreatedAt)
                .ToList();

            var inProgressList = new List<OfferProgressDto>();
            var completedList = new List<OfferProgressDto>();

            foreach (var item in distinctOffers)
            {
                var totalTasks = item.TotalTasks > 0 ? item.TotalTasks : 1;

                if (item.Status == ClickStatus.Completed)
                {
                    completedList.Add(new OfferProgressDto(
                        item.ClickId,
                        item.OfferId,
                        item.Name,
                        item.LogoUrl,
                        item.TotalReward,
                        CompletedTasks: totalTasks,
                        TotalTasks: totalTasks,
                        ProgressPercentage: 100,
                        Status: "COMPLETED",
                        item.CreatedAt
                    ));
                }
                else
                {
                    int completedTasks = 1;
                    int percentage = (int)Math.Round((double)completedTasks / totalTasks * 100);

                    inProgressList.Add(new OfferProgressDto(
                        item.ClickId,
                        item.OfferId,
                        item.Name,
                        item.LogoUrl,
                        item.TotalReward,
                        CompletedTasks: completedTasks,
                        TotalTasks: totalTasks,
                        ProgressPercentage: Math.Min(percentage, 99),
                        Status: "IN_PROGRESS",
                        item.CreatedAt
                    ));
                }
            }

            return new Response(inProgressList, completedList);
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapGet("/user/offers/my", async (
                [FromQuery] int days = 7,
                ISender sender = null!,
                CancellationToken cancellationToken = default) =>
            {
                var query = new Query(days);
                var result = await sender.Send(query, cancellationToken);

                return result.ToHttpResponse();
            }).RequireAuthorization();
        }
    }
}
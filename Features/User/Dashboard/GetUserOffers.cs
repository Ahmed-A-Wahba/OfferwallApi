using Carter;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfferwallApi.Infrastructure.Authorization;
using OfferwallApi.Infrastructure.Interfaces;
using OfferwallApi.Infrastructure.Messaging;
using OfferwallApi.Infrastructure.Persistence;
using OfferwallApi.Infrastructure.Results;
using OfferwallApi.Shared.Entities;

namespace OfferwallApi.Features.User.Dashboard;

public static class GetUserOffers
{
    [Authorize(Role = "User")]
    public sealed record Query(
        string? Platform = null,
        string? DeviceName = null,
        string? Category = null,
        int Page = 1,
        int Limit = 20
    ) : IQuery<Result<Response>>;

    public sealed record Response(
        List<OfferDto> Offers,
        int Total,
        int CurrentPage
    );

    public sealed class Validator : AbstractValidator<Query>
    {
        public Validator()
        {
            RuleFor(x => x.Page)
                .GreaterThan(0)
                .WithMessage("Page must be greater than 0.");

            RuleFor(x => x.Limit)
                .GreaterThan(0)
                .WithMessage("Limit must be greater than 0.")
                .LessThanOrEqualTo(100)
                .WithMessage("Limit cannot exceed 100 items per request.");
        }
    }

    public sealed record OfferDto(
        string OfferId,
        string Title,
        string Description,
        string LogoUrl,
        string Platform,
        string DeviceName,
        decimal PointsReward,
        List<ActivityDto> Activities
    );

    public sealed record ActivityDto(
        string Name,
        string EventId,
        decimal PointsReward
    );

    public sealed class Handler(
        ApplicationDbContext dbContext,
        IKlinkApiClient klinkApiClient,
        IUserLocationService userLocationService,
        ICurrentUser currentUser)
        : IQueryHandler<Query, Result<Response>>
    {
        public async Task<Result<Response>> Handle(Query request, CancellationToken cancellationToken)
        {
            var partner = await dbContext.Partners
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.PartnerId == currentUser.PartnerId, cancellationToken);

            if (partner is null || partner.Status != PublisherStatus.Active)
            {
                return Error.Unauthorized("Partner.Invalid", "Partner is inactive or not found.");
            }

            var country = await userLocationService.GetCountryCodeAsync(cancellationToken);

            System.Console.WriteLine(country);

            var klinkResponse = await klinkApiClient.GetOffersAsync(
                country: "Eg",
                platform: request.Platform,
                deviceName: request.DeviceName,
                category: request.Category?.ToUpper(),
                sortBy: "epc",
                page: request.Page,
                limit: request.Limit,
                cancellationToken: cancellationToken
            );

            if (klinkResponse is null || !klinkResponse.Success)
            {
                return new Response([], 0, request.Page);
            }

            var multiplier = partner.PointsPerUsd * partner.RevenueShareRatio;

            var mappedOffers = klinkResponse.Data.Select(offer => new OfferDto(
                OfferId: offer.OfferId,
                Title: offer.Name?.GetValueOrDefault("en") ?? "Offer",
                Description: offer.Description?.GetValueOrDefault("en") ?? "",
                LogoUrl: offer.Images?.Logo ?? "",
                Platform: offer.Platform,
                DeviceName: offer.DeviceName,
                PointsReward: Math.Round(offer.TotalPayout * multiplier, 2),
                Activities: offer.Activities?.OrderBy(a => a.Order).Select(a => new ActivityDto(
                    Name: a.Name,
                    EventId: a.EventId,
                    PointsReward: Math.Round(a.Payout * multiplier, 2)
                )).ToList() ?? []
            )).ToList();

            return new Response(
                Offers: mappedOffers,
                Total: klinkResponse.Total,
                CurrentPage: klinkResponse.CurrentPage
            );
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapGet("/user/offers", async (
                [FromQuery] string? platform,
                [FromQuery] string? deviceName,
                [FromQuery] string? category,
                [FromQuery] int page = 1,
                [FromQuery] int limit = 20,
                ISender sender = null!,
                CancellationToken cancellationToken = default) =>
            {
                var query = new Query(platform, deviceName, category, page, limit);
                var result = await sender.Send(query, cancellationToken);

                return result.ToHttpResponse();
            }).RequireAuthorization();
        }
    }
}
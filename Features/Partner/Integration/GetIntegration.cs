using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using OfferwallApi.Infrastructure.Authorization;
using OfferwallApi.Infrastructure.Interfaces;
using OfferwallApi.Infrastructure.Persistence;
using OfferwallApi.Infrastructure.Results;

namespace OfferwallApi.Features.Partner.Integration;

public static class GetIntegration
{
    [Authorize(Role = "Partner")]
    public sealed record Query : IRequest<Result<Response>>;

    public sealed record Response(
        Guid PartnerId,
        string ApiKey,
        string ApiSecret,
        decimal PointsPerUsd
    );

    public sealed class Handler(ApplicationDbContext dbContext, ICurrentUser currentUser)
        : IRequestHandler<Query, Result<Response>>
    {
        public async Task<Result<Response>> Handle(Query request, CancellationToken cancellationToken)
        {
            var partnerId = currentUser.UserId;

            var partner = await dbContext.Partners
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.PartnerId == partnerId, cancellationToken);

            if (partner is null)
            {
                return Error.Failure("Partner.NotFound", "Partner integration details were not found.");
            }

            return Result<Response>.Success(new Response(
                partner.PartnerId,
                partner.ApiKey,
                partner.ApiSecret,
                partner.PointsPerUsd
            ));
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapGet("/api/partner/integration", async (
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var query = new Query();
                var result = await sender.Send(query, cancellationToken);

                return result.ToHttpResponse();
            })
            .RequireAuthorization("PartnerPolicy")
            .WithTags("Partner Settings");
        }
    }
}
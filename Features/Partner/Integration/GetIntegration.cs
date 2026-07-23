using Carter;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OfferwallApi.Infrastructure.Authorization;
using OfferwallApi.Infrastructure.Interfaces;
using OfferwallApi.Infrastructure.Messaging;
using OfferwallApi.Infrastructure.Persistence;
using OfferwallApi.Infrastructure.Results;

namespace OfferwallApi.Features.Partner.Integration;

public static class GetIntegration
{
    [Authorize(Role = "Partner")]
    public sealed record Query
    : IQuery<Result<Response>>;

    public sealed record Response(
        Guid PublisherId,
        string ApiKey,
        decimal PointsPerUsd
    );

    [Authorize(Role = "Partner")]
    internal sealed class Handler(
    ApplicationDbContext dbContext,
    ICurrentUser currentUser)
    : IQueryHandler<Query, Result<Response>>
    {
        public async Task<Result<Response>> Handle(
            Query request,
            CancellationToken cancellationToken)
        {
            var partner = await dbContext.Partners
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.PartnerId == currentUser.UserId,
                    cancellationToken);

            if (partner is null)
            {
                return Error.NotFound(
                    "Partner.NotFound",
                    "Partner was not found.");
            }

            return new Response(
                partner.PartnerId,
                partner.ApiKey,
                partner.PointsPerUsd);
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapGet(
                "/partners/integration",
                async (
                    ISender sender) =>
                {
                    var result = await sender.Send(new Query());

                    return result.ToHttpResponse();
                });
        }
    }
}
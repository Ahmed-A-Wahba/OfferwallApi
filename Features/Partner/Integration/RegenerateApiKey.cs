using Carter;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OfferwallApi.Infrastructure.Authorization;
using OfferwallApi.Infrastructure.Interfaces;
using OfferwallApi.Infrastructure.Messaging;
using OfferwallApi.Infrastructure.Persistence;
using OfferwallApi.Infrastructure.Results;

namespace OfferwallApi.Features.Partner.Integration;

public static class RegenerateApiKey
{
    [Authorize(Role = "Partner")]
    public sealed record Command
    : ICommand<Result<Response>>;

    public sealed record Response(
        string ApiKey
    );

    internal sealed class Handler(
    ApplicationDbContext dbContext,
    ICurrentUser currentUser,
    IApiKeyGenerator apiKeyGenerator)
    : ICommandHandler<Command, Result<Response>>
    {
        public async Task<Result<Response>> Handle(
            Command request,
            CancellationToken cancellationToken)
        {
            var partner = await dbContext.Partners
                .FirstOrDefaultAsync(
                    x => x.PartnerId == currentUser.UserId,
                    cancellationToken);

            if (partner is null)
            {
                return Error.NotFound(
                    "Partner.NotFound",
                    "Partner was not found.");
            }

            string apiKey;

            do
            {
                apiKey = apiKeyGenerator.GenerateApiKey();
            }
            while (await dbContext.Partners.AnyAsync(
                x => x.ApiKey == apiKey,
                cancellationToken));

            partner.ApiKey = apiKey;

            await dbContext.SaveChangesAsync(cancellationToken);

            return new Response(apiKey);
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapPost(
                "/partners/integration/regenerate-api-key",
                async (
                    ISender sender) =>
                {
                    var result = await sender.Send(new Command());

                    return result.ToHttpResponse();
                })
                .RequireAuthorization();
        }
    }
}
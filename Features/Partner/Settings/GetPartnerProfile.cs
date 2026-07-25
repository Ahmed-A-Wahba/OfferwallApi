using Carter;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OfferwallApi.Infrastructure.Authorization;
using OfferwallApi.Infrastructure.Interfaces;
using OfferwallApi.Infrastructure.Messaging;
using OfferwallApi.Infrastructure.Persistence;
using OfferwallApi.Infrastructure.Results;

namespace OfferwallApi.Features.Partner.Settings;

public static class GetPartnerProfile
{
    [Authorize(Role = "Partner")]
    public sealed record Query : IQuery<Result<Response>>;

    public sealed record Response(
        string FullName,
        string Email,
        string? PhoneNumber,
        string? LogoUrl);

    internal sealed class Handler(
    ApplicationDbContext context,
    ICurrentUser currentUser)
    : IQueryHandler<Query, Result<Response>>
    {
        public async Task<Result<Response>> Handle(
            Query request,
            CancellationToken cancellationToken)
        {
            var partnerId = currentUser.UserId;

            var partner = await context.Partners
                .AsNoTracking()
                .Where(x => x.PartnerId == partnerId)
                .Select(x => new Response(
                    x.FullName,
                    x.Email,
                    x.PhoneNumber,
                    x.LogoUrl))
                .FirstOrDefaultAsync(cancellationToken);

            if (partner is null)
            {
                return Error.NotFound("PartnerNotFound", "The Partner is not found.");
            }

            return partner;
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapGet(
                "/partner/settings/profile",
                async (
                    ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    var response = await sender.Send(
                        new Query(),
                        cancellationToken);

                    return response.ToHttpResponse();
                });
        }
    }
}

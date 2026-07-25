using Carter;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OfferwallApi.Infrastructure.Authorization;
using OfferwallApi.Infrastructure.Interfaces;
using OfferwallApi.Infrastructure.Messaging;
using OfferwallApi.Infrastructure.Persistence;
using OfferwallApi.Infrastructure.Results;
using OfferwallApi.Shared.Entities;

namespace OfferwallApi.Features.Partner.Settings;

public static class GetPartnerCompany
{
    [Authorize(Role = "Partner")]
    public sealed record Query : IQuery<Result<Response>>;

    public sealed record Response(
    string? LegalName,
    string? TradingName,
    string? RegistrationNumber,
    Country CountryCode,
    string? RegisteredAddress,
    Country TaxResidencyCountry,
    string? TaxRegistrationNumber,

    string? ContactFullName,
    string? ContactEmail,
    string? ContactPhoneNumber);

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

            var company = await context.Partners
                .AsNoTracking()
                .Where(x => x.PartnerId == partnerId)
                .Select(x => new Response(
                    x.LegalName,
                    x.TradingName,
                    x.RegistrationNumber,
                    x.CountryCode,
                    x.RegisteredAddress,
                    x.TaxResidencyCountry,
                    x.TaxRegistrationNumber,

                    x.ContactFullName,
                    x.ContactEmail,
                    x.ContactPhoneNumber))
                .FirstOrDefaultAsync(cancellationToken);

            if (company is null)
            {
                return Error.NotFound(
                    "Partner.NotFound",
                    "Partner was not found.");
            }

            return company;
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapGet(
                "/partner/settings/company",
                async (
                    ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(
                        new Query(),
                        cancellationToken);

                    return result.ToHttpResponse();
                });
        }
    }
}
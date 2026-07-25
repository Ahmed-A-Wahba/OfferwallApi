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

public static class GetPartnerPayment
{
    [Authorize(Role = "Partner")]
    public record Query
    : IQuery<Result<Response>>;

    public sealed record Response(
    PayoutMethod PayoutMethod,
    string? TransferType,
    string? Currency,
    string? AccountName,
    string? SwiftCode,
    string? Iban,
    Country Country,
    string? RoutingNumber,
    string? AccountNumber,
    AccountType AccountType,
    string? AccountHolderName,
    string? AccountHolderAddress,
    string? BankName);

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

            var payment = await context.Partners
                .AsNoTracking()
                .Where(x => x.PartnerId == partnerId)
                .Select(x => new Response(
                    x.PayoutMethod,
                    x.TransferType,
                    x.Currency,
                    x.AccountName,
                    x.SwiftCode,
                    x.Iban,
                    x.CountryCode,
                    x.RoutingNumber,
                    x.AccountNumber,
                    x.AccountType,
                    x.AccountHolderName,
                    x.AccountHolderAddress,
                    x.BankName))
                .FirstOrDefaultAsync(cancellationToken);

            if (payment is null)
            {
                return Error.NotFound(
                    "Partner.NotFound",
                    "Partner was not found.");
            }

            return payment;
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapGet(
                "/partner/settings/payment",
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
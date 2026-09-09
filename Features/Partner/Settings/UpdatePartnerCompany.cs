using Carter;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OfferwallApi.Infrastructure.Authorization;
using OfferwallApi.Infrastructure.Interfaces;
using OfferwallApi.Infrastructure.Messaging;
using OfferwallApi.Infrastructure.Persistence;
using OfferwallApi.Infrastructure.Results;
using OfferwallApi.Shared.Entities;

namespace OfferwallApi.Features.Partner.Settings;

public static class UpdatePartnerCompany
{
    [Authorize(Role = "Partner")]
    public sealed record Command(
        string? LegalName,
        string? TradingName,
        string? IframeOrigin,
        string? RegistrationNumber,
        Country CountryCode,
        string? RegisteredAddress,
        Country TaxResidencyCountry,
        string? TaxRegistrationNumber,

        string? ContactFullName,
        string? ContactEmail,
        string? ContactPhoneNumber
    ) : ICommand<Result>;

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.LegalName)
                .NotEmpty()
                .MaximumLength(200);

            RuleFor(x => x.TradingName)
                .MaximumLength(200);

            RuleFor(x => x.IframeOrigin)
                .MaximumLength(255);

            RuleFor(x => x.RegistrationNumber)
                .MaximumLength(100);

            RuleFor(x => x.RegisteredAddress)
                .MaximumLength(500);

            RuleFor(x => x.TaxRegistrationNumber)
                .MaximumLength(100);

            RuleFor(x => x.ContactFullName)
                .NotEmpty()
                .MaximumLength(100);

            RuleFor(x => x.ContactEmail)
                .NotEmpty()
                .EmailAddress()
                .MaximumLength(255);

            RuleFor(x => x.ContactPhoneNumber)
                .MaximumLength(30);
        }
    }

    internal sealed class Handler(
    ApplicationDbContext context,
    ICurrentUser currentUser)
    : ICommandHandler<Command, Result>
    {
        public async Task<Result> Handle(
            Command request,
            CancellationToken cancellationToken)
        {
            var partnerId = currentUser.UserId;

            var partner = await context.Partners
                .FirstOrDefaultAsync(
                    x => x.PartnerId == partnerId,
                    cancellationToken);

            if (partner is null)
            {
                return Error.NotFound(
                    "Partner.NotFound",
                    "Partner was not found.");
            }

            partner.LegalName = request.LegalName?.Trim();
            partner.TradingName = request.TradingName?.Trim();
            partner.AllowedIframeOrigin = request.IframeOrigin?.Trim();
            partner.RegistrationNumber = request.RegistrationNumber?.Trim();
            partner.CountryCode = request.CountryCode;
            partner.RegisteredAddress = request.RegisteredAddress?.Trim();
            partner.TaxResidencyCountry = request.TaxResidencyCountry;
            partner.TaxRegistrationNumber = request.TaxRegistrationNumber?.Trim();

            partner.ContactFullName = request.ContactFullName?.Trim();
            partner.ContactEmail = request.ContactEmail?.Trim();
            partner.ContactPhoneNumber = request.ContactPhoneNumber?.Trim();

            partner.UpdatedAt = DateTime.UtcNow;

            await context.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapPut(
                "/partner/settings/company",
                async (
                    Command command,
                    ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(
                        command,
                        cancellationToken);

                    return result.ToHttpResponse();
                });
        }
    }
}
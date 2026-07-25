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

public static class UpdatePartnerPayment
{
    [Authorize(Role = "Partner")]
    public sealed record Command(
        PayoutMethod PayoutMethod,
        string? TransferType,
        string? Currency,
        string? AccountName,
        string? SwiftCode,
        string? Iban,
        Country CountryCode,
        string? RoutingNumber,
        string? AccountNumber,
        AccountType AccountType,
        string? AccountHolderName,
        string? AccountHolderAddress,
        string? BankName
    ) : ICommand<Result>;

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.TransferType)
                .MaximumLength(100);

            RuleFor(x => x.Currency)
                .MaximumLength(10);

            RuleFor(x => x.AccountName)
                .MaximumLength(200);

            RuleFor(x => x.SwiftCode)
                .MaximumLength(50);

            RuleFor(x => x.Iban)
                .MaximumLength(100);

            RuleFor(x => x.RoutingNumber)
                .MaximumLength(100);

            RuleFor(x => x.AccountNumber)
                .MaximumLength(100);

            RuleFor(x => x.AccountHolderName)
                .MaximumLength(200);

            RuleFor(x => x.AccountHolderAddress)
                .MaximumLength(500);

            RuleFor(x => x.BankName)
                .MaximumLength(200);
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

            partner.PayoutMethod = request.PayoutMethod;
            partner.TransferType = request.TransferType?.Trim();
            partner.Currency = request.Currency?.Trim();
            partner.AccountName = request.AccountName?.Trim();
            partner.SwiftCode = request.SwiftCode?.Trim();
            partner.Iban = request.Iban?.Trim();
            partner.CountryCode = request.CountryCode;
            partner.RoutingNumber = request.RoutingNumber?.Trim();
            partner.AccountNumber = request.AccountNumber?.Trim();
            partner.AccountType = request.AccountType;
            partner.AccountHolderName = request.AccountHolderName?.Trim();
            partner.AccountHolderAddress = request.AccountHolderAddress?.Trim();
            partner.BankName = request.BankName?.Trim();

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
                "/partner/settings/payment",
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
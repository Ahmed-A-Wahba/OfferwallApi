using Carter;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OfferwallApi.Infrastructure.Authorization;
using OfferwallApi.Infrastructure.Messaging;
using OfferwallApi.Infrastructure.Persistence;
using OfferwallApi.Infrastructure.Results;
using OfferwallApi.Shared.Entities;

namespace OfferwallApi.Features.Admin.Publishers;

public static class GetPublisherDetails
{
    public sealed record Query(Guid PublisherId) : IQuery<Result<Response>>;

    public sealed record Response(
        Guid PublisherId,
        string TradingName,
        string LegalName,
        string? LogoUrl,
        PublisherStatus Status,
        CompanyDetailsDto Company,
        ContactPersonDto Contact,
        PaymentMethodDto? Payment
    );

    public sealed record CompanyDetailsDto(
        string LegalName,
        string TradingName,
        string? RegistrationNumber,
        string? TaxId,
        string? Country,
        string? Address
    );

    public sealed record ContactPersonDto(
        string Name,
        string Email,
        string? Phone,
        string? AvatarUrl
    );

    public sealed record PaymentMethodDto(
        string Type,
        string? BankName,
        string? AccountNumberMasked,
        string? SwiftCode
    );

    internal sealed class Validator : AbstractValidator<Query>
    {
        public Validator()
        {
            RuleFor(x => x.PublisherId).NotEmpty();
        }
    }

    [Authorize(Role = "Admin")]
    internal sealed class Handler(ApplicationDbContext dbContext)
        : IQueryHandler<Query, Result<Response>>
    {
        public async Task<Result<Response>> Handle(Query request, CancellationToken cancellationToken)
        {
            var publisher = await dbContext.Partners
                .AsNoTracking()
                .Where(p => p.PartnerId == request.PublisherId)
                .Select(p => new
                {
                    p.PartnerId,
                    p.CompanyName,
                    p.LegalName,
                    p.TradingName,
                    p.RegistrationNumber,
                    p.TaxRegistrationNumber,
                    CountryCode = p.CountryCode.ToString(),
                    p.RegisteredAddress,
                    p.LogoUrl,
                    p.Status,
                    p.CreatedAt,


                    ContactName = p.ContactFullName ?? p.FullName,
                    ContactEmail = p.ContactEmail ?? p.Email,
                    ContactPhone = p.ContactPhoneNumber ?? p.PhoneNumber,

                    PayoutMethod = p.PayoutMethod.ToString(),
                    TransferType = p.TransferType,
                    p.BankName,
                    p.AccountNumber,
                    p.Iban,
                    p.SwiftCode
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (publisher is null)
                return Error.NotFound("Publisher.NotFound", "The requested publisher was not found.");


            string? rawAccount = !string.IsNullOrWhiteSpace(publisher.AccountNumber)
                ? publisher.AccountNumber
                : publisher.Iban;

            var response = new Response(
                PublisherId: publisher.PartnerId,
                TradingName: publisher.TradingName ?? publisher.CompanyName,
                LegalName: publisher.LegalName ?? publisher.CompanyName,
                LogoUrl: publisher.LogoUrl,
                Status: publisher.Status,

                Company: new CompanyDetailsDto(
                    LegalName: publisher.LegalName ?? publisher.CompanyName,
                    TradingName: publisher.TradingName ?? publisher.CompanyName,
                    RegistrationNumber: publisher.RegistrationNumber,
                    TaxId: publisher.TaxRegistrationNumber,
                    Country: publisher.CountryCode,
                    Address: publisher.RegisteredAddress
                ),

                Contact: new ContactPersonDto(
                    Name: publisher.ContactName,
                    Email: publisher.ContactEmail,
                    Phone: publisher.ContactPhone,
                    AvatarUrl: null
                ),

                Payment: new PaymentMethodDto(
                    Type: !string.IsNullOrWhiteSpace(publisher.TransferType)
                        ? publisher.TransferType
                        : publisher.PayoutMethod,
                    BankName: publisher.BankName,
                    AccountNumberMasked: rawAccount,
                    SwiftCode: publisher.SwiftCode
                )
            );

            return response;
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapGet("/admin/publishers/{id:guid}", async (
                Guid id,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var query = new Query(id);
                var result = await sender.Send(query, cancellationToken);

                return result.ToHttpResponse();
            })
            .RequireAuthorization();
        }
    }
}
using Carter;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OfferwallApi.Infrastructure.Authorization;
using OfferwallApi.Infrastructure.Messaging;
using OfferwallApi.Infrastructure.Persistence;
using OfferwallApi.Infrastructure.Results;

namespace OfferwallApi.Features.Admin.Publishers;

public static class GetPublisherPaymentDetails
{
    [Authorize(Role = "Admin")]
    public sealed record Query(Guid PublisherId) : IQuery<Result<Response>>;

    public sealed record Response(
        Guid PublisherId,
        string DisplayId,
        string? PublisherName,
        string? AccountType,
        string? Method,
        string? BankName,
        string? AccountHolder,
        string? AccountNumber,
        string? SwiftCode,
        string? RoutingNumber,
        string? Currency,
        string? BankAddress
    );

    internal sealed class Validator : AbstractValidator<Query>
    {
        public Validator()
        {
            RuleFor(x => x.PublisherId).NotEmpty();
        }
    }

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
                    PublisherName = p.CompanyName,
                    AccountType = p.AccountType.ToString().ToUpper(),
                    Method = p.TransferType,
                    p.BankName,
                    AccountHolder = p.AccountHolderName,
                    AccountNumber = p.AccountNumber,
                    p.SwiftCode,
                    p.RoutingNumber,
                    Currency = p.Currency,
                    BankAddress = p.AccountHolderAddress
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (publisher is null)
                return Error.NotFound("Publisher.NotFound", "The requested publisher was not found.");

            var response = new Response(
                PublisherId: publisher.PartnerId,
                DisplayId: $"PUB-{publisher.PartnerId.ToString().Substring(0, 5).ToUpper()}",
                PublisherName: publisher.PublisherName,
                AccountType: publisher.AccountType,
                Method: publisher.Method,
                BankName: publisher.BankName,
                AccountHolder: publisher.AccountHolder,
                AccountNumber: publisher.AccountNumber,
                SwiftCode: publisher.SwiftCode,
                RoutingNumber: publisher.RoutingNumber,
                Currency: publisher.Currency,
                BankAddress: publisher.BankAddress
            );

            return response;
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapGet("/admin/publishers/{id:guid}/payment-details", async (
                Guid id,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var query = new Query(id);
                var result = await sender.Send(query, cancellationToken);

                return result.ToHttpResponse();
            });
        }
    }
}
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

public static class GetPublishers
{
    public sealed record Query(
        int PageNumber = 1,
        int PageSize = 10,
        string? SearchTerm = null,
        PublisherStatus? Status = null
    ) : IQuery<Result<Response>>;

    public sealed record Response(
        List<PublisherItem> Items,
        int TotalCount,
        int PageNumber,
        int PageSize,
        int PendingCount
    );

    public sealed record PublisherItem(
        Guid PublisherId,
        string Name,
        string CompanyName,
        string? LogoUrl,
        PublisherStatus Status,
        decimal TotalRevenue,
        int CompletedOffers,
        DateTime RegisteredAt
    );

    internal sealed class Validator : AbstractValidator<Query>
    {
        public Validator()
        {
            RuleFor(x => x.PageNumber).GreaterThanOrEqualTo(1);
            RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        }
    }

    [Authorize(Role = "Admin")]
    internal sealed class Handler(ApplicationDbContext dbContext)
        : IQueryHandler<Query, Result<Response>>
    {
        public async Task<Result<Response>> Handle(Query request, CancellationToken cancellationToken)
        {
            var query = dbContext.Partners.AsNoTracking();

            var pendingCount = await dbContext.Partners
                .CountAsync(x => x.Status == PublisherStatus.PendingApproval, cancellationToken);

            if (request.Status.HasValue)
            {
                query = query.Where(x => x.Status == request.Status.Value);
            }

            if (!string.IsNullOrWhiteSpace(request.SearchTerm))
            {
                var term = request.SearchTerm.Trim().ToLower();
                query = query.Where(x =>
                    x.CompanyName.ToLower().Contains(term) ||
                    (x.AccountName != null && x.AccountName.ToLower().Contains(term)));
            }

            var totalCount = await query.CountAsync(cancellationToken);

            var items = await query
                .OrderByDescending(x => x.CreatedAt)
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(x => new PublisherItem(
                    x.PartnerId,
                    x.CompanyName,
                    x.CompanyName ?? string.Empty,
                    x.LogoUrl,
                    x.Status,
                    dbContext.Conversions
                        .Where(c => c.PartnerId == x.PartnerId && c.Type == ConversionType.Conversion)
                        .Sum(c => (decimal?)c.Payout) ?? 0,
                    dbContext.Conversions
                        .Count(c => c.PartnerId == x.PartnerId && c.Type == ConversionType.Conversion),
                    x.CreatedAt
                ))
                .ToListAsync(cancellationToken);

            return new Response(
                items,
                totalCount,
                request.PageNumber,
                request.PageSize,
                pendingCount
            );
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapGet("/admin/publishers", async (
                [AsParameters] Query query,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(query, cancellationToken);
                return result.ToHttpResponse();
            })
            .RequireAuthorization();
        }
    }
}
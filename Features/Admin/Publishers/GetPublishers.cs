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
    [Authorize(Role = "Admin")]
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

    internal sealed class Handler(ApplicationDbContext dbContext)
        : IQueryHandler<Query, Result<Response>>
    {
        public async Task<Result<Response>> Handle(Query request, CancellationToken cancellationToken)
        {
            var pendingCount = await dbContext.Partners
                .CountAsync(x => x.Status == PublisherStatus.PendingApproval, cancellationToken);

            var query = dbContext.Partners.AsNoTracking();

            if (request.Status.HasValue)
            {
                query = query.Where(x => x.Status == request.Status.Value);
            }

            if (!string.IsNullOrWhiteSpace(request.SearchTerm))
            {
                var term = request.SearchTerm.Trim();
                query = query.Where(x =>
                    EF.Functions.Like(x.CompanyName, $"%{term}%") ||
                    (x.AccountName != null && EF.Functions.Like(x.AccountName, $"%{term}%")));
            }

            var totalCount = await query.CountAsync(cancellationToken);

            // 2. سحب الصفحة المطلوبة فقط من جدول الـ Partners
            var partnersPage = await query
                .OrderByDescending(x => x.CreatedAt)
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(x => new
                {
                    x.PartnerId,
                    x.AccountName,
                    x.CompanyName,
                    x.LogoUrl,
                    x.Status,
                    x.RevenueShareRatio,
                    x.CreatedAt
                })
                .ToListAsync(cancellationToken);

            var partnerIds = partnersPage.Select(p => p.PartnerId).ToList();

            var conversionsStats = await dbContext.Conversions
                .AsNoTracking()
                .Where(c => partnerIds.Contains(c.PartnerId) && c.Type == ConversionType.Conversion)
                .Select(c => new
                {
                    c.PartnerId,
                    Payout = Math.Abs(c.Payout)
                })
                .ToListAsync(cancellationToken);

            var statsLookup = conversionsStats
                .GroupBy(c => c.PartnerId)
                .ToDictionary(
                    g => g.Key,
                    g => new
                    {
                        CompletedCount = g.Count(),
                        TotalPayout = g.Sum(x => x.Payout)
                    }
                );

            var items = partnersPage.Select(p =>
            {
                var hasStats = statsLookup.TryGetValue(p.PartnerId, out var stat);
                var completedOffers = hasStats ? stat!.CompletedCount : 0;
                var publisherPayout = hasStats ? stat!.TotalPayout : 0m;

                var profit = CalculateProfit(publisherPayout, p.RevenueShareRatio);
                var totalRevenue = publisherPayout + profit;

                return new PublisherItem(
                    PublisherId: p.PartnerId,
                    Name: p.AccountName ?? p.CompanyName,
                    CompanyName: p.CompanyName ?? string.Empty,
                    LogoUrl: p.LogoUrl,
                    Status: p.Status,
                    TotalRevenue: totalRevenue,
                    CompletedOffers: completedOffers,
                    RegisteredAt: p.CreatedAt
                );
            }).ToList();

            return new Response(
                items,
                totalCount,
                request.PageNumber,
                request.PageSize,
                pendingCount
            );
        }

        private static decimal CalculateProfit(decimal publisherPayout, decimal shareRatio)
        {
            if (shareRatio <= 0 || shareRatio >= 1) return 0m;

            var total = publisherPayout / shareRatio;
            return Math.Round(total - publisherPayout, 4, MidpointRounding.AwayFromZero);
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
            });
        }
    }
}
using Carter;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OfferwallApi.Infrastructure.Authorization;
using OfferwallApi.Infrastructure.Messaging;
using OfferwallApi.Infrastructure.Persistence;
using OfferwallApi.Infrastructure.Results;
using OfferwallApi.Shared.Entities;

namespace OfferwallApi.Features.Admin.Payouts;

public static class GetPagedPayouts
{
    [Authorize(Role = "Admin")]
    public sealed record Query(
        Guid? PartnerId,
        string? Status,
        DateTime? StartDate,
        DateTime? EndDate,
        int PageNumber = 1,
        int PageSize = 10
    ) : IQuery<Result<Response>>;

    public sealed record Response(
        int TotalCount,
        List<PayoutItemDto> Payouts
    );

    public sealed record PayoutItemDto(
        Guid PayoutId,
        string PublisherName,
        string PublisherCompany,
        string Period,
        decimal Amount,
        string PaymentMethod,
        DateTime ScheduledDate,
        string Status,
        PayoutBreakdownDto Breakdown
    );

    public sealed record PayoutBreakdownDto(
        int TotalRedemptions,
        int TotalPoints,
        string RevenueShare,
        decimal TotalRevenue,
        decimal Chargebacks,
        decimal NetProfit
    );

    internal sealed class Validator : AbstractValidator<Query>
    {
        public Validator()
        {
            RuleFor(x => x.PageNumber).GreaterThanOrEqualTo(1);
            RuleFor(x => x.PageSize).GreaterThan(0);
        }
    }

    internal sealed class Handler(ApplicationDbContext dbContext)
        : IQueryHandler<Query, Result<Response>>
    {
        public async Task<Result<Response>> Handle(Query request, CancellationToken cancellationToken)
        {
            var query = dbContext.Payouts
                .AsNoTracking()
                .AsQueryable();

            // 1. تطبيق الفلاتر
            if (request.PartnerId.HasValue && request.PartnerId.Value != Guid.Empty)
                query = query.Where(p => p.PartnerId == request.PartnerId);

            if (!string.IsNullOrWhiteSpace(request.Status) && !request.Status.Equals("All Statuses", StringComparison.OrdinalIgnoreCase))
            {
                if (Enum.TryParse<PayoutStatus>(request.Status, true, out var parsedStatus))
                    query = query.Where(p => p.Status == parsedStatus);
            }

            if (request.StartDate.HasValue)
                query = query.Where(p => p.PeriodStart >= request.StartDate.Value);

            if (request.EndDate.HasValue)
                query = query.Where(p => p.PeriodEnd <= request.EndDate.Value);

            int totalCount = await query.CountAsync(cancellationToken);

            // 2. جلب البيانات واسقاط الحقول المطلوبة (Projection)
            var pagedData = await query
                .OrderByDescending(p => p.CreatedAt)
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(p => new
                {
                    p.PayoutId,
                    p.Amount,
                    p.RedemptionCount,
                    p.PeriodStart,
                    p.PeriodEnd,
                    p.Status,
                    p.CreatedAt,
                    CompanyName = p.Partner != null ? p.Partner.CompanyName : "Unknown",
                    AccountName = p.Partner != null ? p.Partner.AccountName : null,
                    RevenueShareRatio = p.Partner != null ? p.Partner.RevenueShareRatio : 0.85m,
                    PayoutMethod = p.Partner != null
                        ? p.Partner.PayoutMethod.ToString()
                        : "Bank Transfer"
                })
                .ToListAsync(cancellationToken);

            var payoutsList = pagedData.Select(p =>
            {
                var publisherPayout = Math.Abs(p.Amount);
                var netProfit = CalculateProfit(publisherPayout, p.RevenueShareRatio);
                var totalRevenue = publisherPayout + netProfit;

                var sharePercentageStr = $"{Math.Round(p.RevenueShareRatio * 100, 1)}%";
                var publisherName = p.AccountName ?? p.CompanyName;

                return new PayoutItemDto(
                    PayoutId: p.PayoutId,
                    PublisherName: publisherName,
                    PublisherCompany: p.CompanyName,
                    Period: $"{p.PeriodStart:MMM d} - {p.PeriodEnd:MMM d, yyyy}",
                    Amount: publisherPayout,
                    PaymentMethod: p.PayoutMethod,
                    ScheduledDate: p.PeriodEnd.AddMonths(1).Date,
                    Status: MapStatusForUi(p.Status),
                    Breakdown: new PayoutBreakdownDto(
                        TotalRedemptions: p.RedemptionCount,
                        TotalPoints: p.RedemptionCount * 1000,
                        RevenueShare: sharePercentageStr,
                        TotalRevenue: totalRevenue,
                        Chargebacks: 0.00m,
                        NetProfit: netProfit
                    )
                );
            }).ToList();

            return new Response(totalCount, payoutsList);
        }

        private static decimal CalculateProfit(decimal publisherPayout, decimal shareRatio)
        {
            if (shareRatio <= 0 || shareRatio >= 1) return 0m;

            var total = publisherPayout / shareRatio;
            return Math.Round(total - publisherPayout, 4, MidpointRounding.AwayFromZero);
        }

        private static string MapStatusForUi(PayoutStatus status) => status switch
        {
            PayoutStatus.Pending => "SCHEDULED",
            PayoutStatus.Paid => "PAID",
            PayoutStatus.Cancelled => "FAILED",
            _ => status.ToString().ToUpper()
        };
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapGet("/admin/payouts", async (
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
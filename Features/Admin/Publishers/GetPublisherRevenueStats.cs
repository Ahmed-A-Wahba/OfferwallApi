using Carter;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfferwallApi.Infrastructure.Authorization;
using OfferwallApi.Infrastructure.Messaging;
using OfferwallApi.Infrastructure.Persistence;
using OfferwallApi.Infrastructure.Results;
using OfferwallApi.Shared.Entities;

namespace OfferwallApi.Features.Admin.Publishers;

public static class GetPublisherRevenueStats
{
    public sealed record Query(
        Guid PublisherId,
        DateTime? FromDate = null,
        DateTime? ToDate = null
    ) : IQuery<Result<Response>>;

    public sealed record Response(
        bool PublisherAbsorbsChargebacks,
        decimal TotalRevenue,
        decimal TotalProfit,
        decimal PendingRevenue,
        int NextPayoutInDays,
        decimal TotalChargebacks,
        List<RevenueChartDataPoint> RevenueGrowthChart,
        List<TransferRecordDto> TransferRecords
    );

    public sealed record RevenueChartDataPoint(
        string MonthLabel,
        decimal Value
    );

    public sealed record TransferRecordDto(
        Guid PayoutId,
        DateTime TransferDate,
        string Period,
        decimal Amount,
        string Method,
        string ReferenceNumber,
        string? ProofUrl
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
            var partner = await dbContext.Partners
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.PartnerId == request.PublisherId, cancellationToken);

            if (partner is null)
                return Error.NotFound("Publisher.NotFound", "Publisher not found.");

            var fromDate = request.FromDate ?? DateTime.UtcNow.AddMonths(-6);
            var toDate = request.ToDate ?? DateTime.UtcNow;

            var conversionsQuery = dbContext.Conversions
                .AsNoTracking()
                .Where(c => c.PartnerId == request.PublisherId && c.CreatedAt >= fromDate && c.CreatedAt <= toDate);

            decimal totalRevenue = await conversionsQuery
                .Where(c => c.Type == ConversionType.Conversion)
                .SumAsync(c => (decimal?)c.Payout, cancellationToken) ?? 0m;

            decimal totalProfit = totalRevenue * (1 - partner.RevenueShareRatio);

            decimal totalChargebacks = await conversionsQuery
                .Where(c => c.Type == ConversionType.Chargeback)
                .SumAsync(c => (decimal?)c.Payout, cancellationToken) ?? 0m;

            decimal pendingRevenue = await dbContext.Payouts
                .AsNoTracking()
                .Where(p => p.PartnerId == request.PublisherId && p.Status == PayoutStatus.Pending)
                .SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;

            var endDate = request.ToDate ?? DateTime.UtcNow;
            var last6Months = Enumerable.Range(0, 6)
                .Select(i => endDate.AddMonths(-i))
                .Select(d => new { d.Year, d.Month })
                .Reverse()
                .ToList();

            var chartData = await conversionsQuery
                .Where(c => c.Type == ConversionType.Conversion)
                .GroupBy(c => new { c.CreatedAt.Year, c.CreatedAt.Month })
                .Select(g => new
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    Total = g.Sum(c => c.Payout)
                })
                .ToListAsync(cancellationToken);

            var revenueChart = last6Months
                .Select(m => new RevenueChartDataPoint(
                    MonthLabel: new DateTime(m.Year, m.Month, 1).ToString("MMM").ToUpper(),
                    Value: chartData.FirstOrDefault(c => c.Year == m.Year && c.Month == m.Month)?.Total ?? 0m
                ))
                .ToList();

            var payouts = await dbContext.Payouts
                .AsNoTracking()
                .Where(p => p.PartnerId == request.PublisherId)
                .OrderByDescending(p => p.CreatedAt)
                .Take(10)
                .Select(p => new TransferRecordDto(
                    p.PayoutId,
                    p.CreatedAt,
                    $"{p.PeriodStart:MMM d}–{p.PeriodEnd:MMM d, yyyy}",
                    p.Amount,
                    partner.PayoutMethod.ToString(),
                    $"TRF-{p.CreatedAt:yyyy}-{p.PayoutId.ToString().Substring(0, 5).ToUpper()}",
                    null
                ))
                .ToListAsync(cancellationToken);

            var response = new Response(
                PublisherAbsorbsChargebacks: partner.HasChargebacks,
                TotalRevenue: totalRevenue,
                TotalProfit: totalProfit,
                PendingRevenue: pendingRevenue,
                NextPayoutInDays: 12,
                TotalChargebacks: totalChargebacks,
                RevenueGrowthChart: revenueChart,
                TransferRecords: payouts
            );

            return response;
        }
    }

    // 5. Endpoint
    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapGet("/admin/publishers/{id:guid}/revenue-stats", async (
                Guid id,
                [FromQuery] DateTime? fromDate,
                [FromQuery] DateTime? toDate,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var query = new Query(id, fromDate, toDate);
                var result = await sender.Send(query, cancellationToken);
                return result.ToHttpResponse();
            })
            .RequireAuthorization();
        }
    }
}
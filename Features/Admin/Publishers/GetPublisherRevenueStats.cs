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

public static class GetPublisherRevenueStats
{
    [Authorize(Role = "Admin")]
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

            var conversions = await dbContext.Conversions
                .AsNoTracking()
                .Where(c => c.PartnerId == request.PublisherId && c.CreatedAt >= fromDate && c.CreatedAt <= toDate)
                .Select(c => new
                {
                    Payout = Math.Abs(c.Payout),
                    c.Type,
                    c.CreatedAt
                })
                .ToListAsync(cancellationToken);

            var validConversions = conversions.Where(c => c.Type == ConversionType.Conversion).ToList();


            decimal publisherPayout = validConversions.Sum(c => c.Payout);
            decimal totalProfit = CalculateProfit(publisherPayout, partner.RevenueShareRatio);
            decimal totalRevenue = publisherPayout + totalProfit;

            decimal totalChargebacks = conversions
                .Where(c => c.Type == ConversionType.Chargeback)
                .Sum(c => c.Payout);

            decimal pendingRevenue = await dbContext.Payouts
                .AsNoTracking()
                .Where(p => p.PartnerId == request.PublisherId && p.Status == PayoutStatus.Pending)
                .SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;


            var last6Months = Enumerable.Range(0, 6)
                .Select(i => toDate.AddMonths(-i))
                .Select(d => new { d.Year, d.Month })
                .Reverse()
                .ToList();

            var chartDataLookup = validConversions
                .GroupBy(c => new { c.CreatedAt.Year, c.CreatedAt.Month })
                .ToDictionary(
                    g => g.Key,
                    g =>
                    {
                        var payoutSum = g.Sum(x => x.Payout);
                        return payoutSum + CalculateProfit(payoutSum, partner.RevenueShareRatio);
                    }
                );

            var revenueChart = last6Months
                .Select(m =>
                {
                    var key = new { m.Year, m.Month };
                    var val = chartDataLookup.TryGetValue(key, out var monthlyTotal) ? monthlyTotal : 0m;
                    return new RevenueChartDataPoint(
                        MonthLabel: new DateTime(m.Year, m.Month, 1).ToString("MMM").ToUpper(),
                        Value: val
                    );
                })
                .ToList();

            var payouts = await dbContext.Payouts
                .AsNoTracking()
                .Where(p => p.PartnerId == request.PublisherId)
                .OrderByDescending(p => p.CreatedAt)
                .Take(10)
                .Select(p => new
                {
                    p.PayoutId,
                    p.CreatedAt,
                    p.PeriodStart,
                    p.PeriodEnd,
                    p.Amount
                })
                .ToListAsync(cancellationToken);

            string payoutMethodName = partner.PayoutMethod.ToString() ?? "N/A";

            var transferRecords = payouts.Select(p => new TransferRecordDto(
                PayoutId: p.PayoutId,
                TransferDate: p.CreatedAt,
                Period: $"{p.PeriodStart:MMM d}–{p.PeriodEnd:MMM d, yyyy}",
                Amount: p.Amount,
                Method: payoutMethodName,
                ReferenceNumber: $"TRF-{p.CreatedAt:yyyy}-{p.PayoutId.ToString()[..5].ToUpper()}",
                ProofUrl: null
            )).ToList();

            var response = new Response(
                PublisherAbsorbsChargebacks: partner.HasChargebacks,
                TotalRevenue: totalRevenue,
                TotalProfit: totalProfit,
                PendingRevenue: pendingRevenue,
                NextPayoutInDays: 12,
                TotalChargebacks: totalChargebacks,
                RevenueGrowthChart: revenueChart,
                TransferRecords: transferRecords
            );

            return response;
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
            app.MapGet("/admin/publishers/{publisherId:guid}/revenue-stats", async (
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
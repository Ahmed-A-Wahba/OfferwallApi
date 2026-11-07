using Carter;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OfferwallApi.Infrastructure.Authorization;
using OfferwallApi.Infrastructure.Messaging;
using OfferwallApi.Infrastructure.Persistence;
using OfferwallApi.Infrastructure.Results;
using OfferwallApi.Shared.Entities;

namespace OfferwallApi.Features.Admin.Payouts;

public static class GetPayoutStats
{
    [Authorize(Role = "Admin")]
    public sealed record Query() : IQuery<Result<Response>>;

    public sealed record Response(
        decimal TotalPendingPayout,
        int PendingPublishersCount,
        decimal TotalPaidThisMonth,
        decimal MonthOverMonthGrowth,
        DateTime NextPayoutBatch,
        int AwaitingPaymentCount
    );

    internal sealed class Handler(ApplicationDbContext dbContext)
        : IQueryHandler<Query, Result<Response>>
    {
        public async Task<Result<Response>> Handle(Query request, CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;
            var startOfThisMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var startOfLastMonth = startOfThisMonth.AddMonths(-1);

            // 1. حساب المبالغ المعلقة وإحصائياتها بنشاط وحماية للـ Memory عبر SQL Server
            var pendingQuery = dbContext.Payouts
                .AsNoTracking()
                .Where(p => p.Status == PayoutStatus.Pending);

            var totalPendingAmount = await pendingQuery
                .SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;

            var pendingPublishersCount = await pendingQuery
                .Select(p => p.PartnerId)
                .Distinct()
                .CountAsync(cancellationToken);

            var awaitingPaymentCount = await pendingQuery.CountAsync(cancellationToken);

            // 2. حساب المبالغ المدفوعة هذا الشهر والشهر الماضي
            var paidThisMonth = await dbContext.Payouts
                .AsNoTracking()
                .Where(p => p.Status == PayoutStatus.Paid && p.PaidAt >= startOfThisMonth)
                .SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;

            var paidLastMonth = await dbContext.Payouts
                .AsNoTracking()
                .Where(p => p.Status == PayoutStatus.Paid && p.PaidAt >= startOfLastMonth && p.PaidAt < startOfThisMonth)
                .SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;

            // 3. حساب نسبة النمو (Growth Rate)
            decimal growth = CalculatePercentage(paidThisMonth, paidLastMonth);

            // 4. موعد الدفعة القادمة (أول يوم في الشهر القادم)
            var nextPayoutBatch = startOfThisMonth.AddMonths(1);

            return new Response(
                TotalPendingPayout: totalPendingAmount,
                PendingPublishersCount: pendingPublishersCount,
                TotalPaidThisMonth: paidThisMonth,
                MonthOverMonthGrowth: growth,
                NextPayoutBatch: nextPayoutBatch,
                AwaitingPaymentCount: awaitingPaymentCount
            );
        }

        private static decimal CalculatePercentage(decimal current, decimal previous)
        {
            if (previous == 0) return current > 0 ? 100m : 0m;
            var change = ((current - previous) / previous) * 100m;
            return Math.Round(change, 1);
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapGet("/admin/payouts/stats", async (
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new Query(), cancellationToken);
                return result.ToHttpResponse();
            });
        }
    }
}
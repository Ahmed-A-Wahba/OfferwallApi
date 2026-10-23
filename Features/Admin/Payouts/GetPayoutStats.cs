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
    public sealed record Query() : IQuery<Result<Response>>;

    public sealed record Response(
        decimal TotalPendingPayout,
        int PendingPublishersCount,
        decimal TotalPaidThisMonth,
        decimal MonthOverMonthGrowth, // نسبة التغيير
        DateTime NextPayoutBatch,
        int AwaitingPaymentCount
    );

    [Authorize(Role = "Admin")]
    internal sealed class Handler(ApplicationDbContext dbContext)
        : IQueryHandler<Query, Result<Response>>
    {
        public async Task<Result<Response>> Handle(Query request, CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;
            var startOfThisMonth = new DateTime(now.Year, now.Month, 1);
            var startOfLastMonth = startOfThisMonth.AddMonths(-1);

            // 1. حساب المبالغ المعلقة (Pending)
            var pendingPayouts = await dbContext.Payouts
                .Where(p => p.Status == PayoutStatus.Pending)
                .Select(p => new { p.Amount, p.PartnerId })
                .ToListAsync(cancellationToken);

            var totalPendingAmount = pendingPayouts.Sum(p => p.Amount);
            var pendingPublishersCount = pendingPayouts.Select(p => p.PartnerId).Distinct().Count();

            // 2. حساب المبالغ المدفوعة هذا الشهر والشهر الماضي لنسبة النمو
            var paidThisMonth = await dbContext.Payouts
                .Where(p => p.Status == PayoutStatus.Paid && p.PaidAt >= startOfThisMonth)
                .SumAsync(p => p.Amount, cancellationToken);

            var paidLastMonth = await dbContext.Payouts
                .Where(p => p.Status == PayoutStatus.Paid && p.PaidAt >= startOfLastMonth && p.PaidAt < startOfThisMonth)
                .SumAsync(p => p.Amount, cancellationToken);

            // حساب نسبة النمو
            decimal growth = paidLastMonth == 0 ? 0 : ((paidThisMonth - paidLastMonth) / paidLastMonth) * 100;

            // 3. موعد الدفعة القادمة (عادة تكون أول يوم في الشهر القادم لنظام Net30)
            var nextPayoutBatch = startOfThisMonth.AddMonths(1);

            return new Response(
                TotalPendingPayout: totalPendingAmount,
                PendingPublishersCount: pendingPublishersCount,
                TotalPaidThisMonth: paidThisMonth,
                MonthOverMonthGrowth: Math.Round(growth, 1),
                NextPayoutBatch: nextPayoutBatch,
                AwaitingPaymentCount: pendingPayouts.Count // عدد الفواتير المعلقة
            );
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
            })
            .RequireAuthorization();
        }
    }
}
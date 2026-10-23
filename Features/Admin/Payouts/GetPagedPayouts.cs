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
                .Include(p => p.Partner)
                .AsNoTracking()
                .AsQueryable();

            // 1. تطبيق الفلاتر
            if (request.PartnerId.HasValue && request.PartnerId.Value != Guid.Empty)
                query = query.Where(p => p.PartnerId == request.PartnerId);

            if (!string.IsNullOrWhiteSpace(request.Status) && request.Status != "All Statuses")
            {
                if (Enum.TryParse<PayoutStatus>(request.Status, true, out var parsedStatus))
                    query = query.Where(p => p.Status == parsedStatus);
            }

            if (request.StartDate.HasValue)
                query = query.Where(p => p.PeriodStart >= request.StartDate.Value);

            if (request.EndDate.HasValue)
                query = query.Where(p => p.PeriodEnd <= request.EndDate.Value);

            int totalCount = await query.CountAsync(cancellationToken);

            // 2. جلب البيانات
            var pagedData = await query
                .OrderByDescending(p => p.CreatedAt)
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync(cancellationToken);

            // 3. تحويل البيانات (Mapping) لتطابق واجهة المستخدم
            var payoutsList = pagedData.Select(p =>
            {
                // حسابات افتراضية للـ Breakdown (يمكنك استبدالها لاحقاً بحقول حقيقية من الـ DB)
                decimal totalRevenue = p.Amount / 0.85m; // افتراض نسبة ربح 85% للناشر
                decimal netProfit = totalRevenue - p.Amount;
                int estimatedPoints = p.RedemptionCount * 1000; // مجرد رقم تقريبي لعرضه

                return new PayoutItemDto(
                    PayoutId: p.PayoutId,
                    PublisherName: p.Partner?.CompanyName ?? "Unknown",
                    PublisherCompany: $"{p.Partner?.CompanyName}",
                    Period: $"{p.PeriodStart:MMM d} - {p.PeriodEnd:MMM d, yyyy}",
                    Amount: p.Amount,
                    PaymentMethod: "Bank Transfer", // Mock (يجب إضافته في جدول الـ Partner لاحقاً)
                    ScheduledDate: p.PeriodEnd.AddMonths(1).Date, // Net30
                    Status: MapStatusForUi(p.Status),
                    Breakdown: new PayoutBreakdownDto(
                        TotalRedemptions: p.RedemptionCount,
                        TotalPoints: estimatedPoints,
                        RevenueShare: "85%", // Mock
                        TotalRevenue: totalRevenue,
                        Chargebacks: 0.00m, // Mock
                        NetProfit: netProfit
                    )
                );
            }).ToList();

            return new Response(totalCount, payoutsList);
        }

        // دالة لتحويل حالة الداتابيز لنصوص تطابق الـ UI Badges
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
                [FromQuery] Guid? partnerId,
                [FromQuery] string? status,
                [FromQuery] DateTime? startDate,
                [FromQuery] DateTime? endDate,
                [FromQuery] int pageNumber,
                [FromQuery] int pageSize,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                pageNumber = pageNumber > 0 ? pageNumber : 1;
                pageSize = pageSize > 0 ? pageSize : 10;

                var query = new Query(partnerId, status, startDate, endDate, pageNumber, pageSize);
                var result = await sender.Send(query, cancellationToken);

                return result.ToHttpResponse();
            })
            .RequireAuthorization();
        }
    }
}
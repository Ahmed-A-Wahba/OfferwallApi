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

namespace OfferwallApi.Features.Admin.Users;

public static class GetUserActivityLog
{
    // 1. Query
    public sealed record Query(
        Guid UserId,
        string? EventType, // مثلاً "All Events", "OfferReward", "CouponCreation"
        DateTime? StartDate,
        DateTime? EndDate,
        int PageNumber = 1,
        int PageSize = 10
    ) : IQuery<Result<Response>>;

    // 2. Response DTOs
    public sealed record Response(
        int TotalCount,
        List<ActivityItemDto> Activities
    );

    public sealed record ActivityItemDto(
        DateTime Timestamp,
        string EventType,
        string Details,
        decimal PointsChange,
        string Status
    );

    // 3. Validator
    internal sealed class Validator : AbstractValidator<Query>
    {
        public Validator()
        {
            RuleFor(x => x.UserId).NotEmpty();
            RuleFor(x => x.PageNumber).GreaterThanOrEqualTo(1);
            RuleFor(x => x.PageSize).GreaterThan(0);
        }
    }

    // 4. Handler
    [Authorize(Role = "Admin")]
    internal sealed class Handler(ApplicationDbContext dbContext)
        : IQueryHandler<Query, Result<Response>>
    {
        public async Task<Result<Response>> Handle(Query request, CancellationToken cancellationToken)
        {
            var query = dbContext.WalletTransactions
                .AsNoTracking()
                .Where(wt => wt.UserId == request.UserId);

            // تطبيق فلاتر التاريخ
            if (request.StartDate.HasValue)
                query = query.Where(wt => wt.CreatedAt >= request.StartDate.Value);

            if (request.EndDate.HasValue)
                query = query.Where(wt => wt.CreatedAt <= request.EndDate.Value);

            // تطبيق فلتر نوع الحدث
            if (!string.IsNullOrWhiteSpace(request.EventType) && request.EventType != "All Events")
            {
                if (Enum.TryParse<WalletTransactionType>(request.EventType, true, out var parsedType))
                {
                    query = query.Where(wt => wt.Type == parsedType);
                }
            }

            int totalCount = await query.CountAsync(cancellationToken);

            // جلب البيانات مع الروابط (Coupon & Conversion) لتكوين عمود التفاصيل بشكل ديناميكي
            var pagedData = await query
                .OrderByDescending(wt => wt.CreatedAt)
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(wt => new
                {
                    wt.CreatedAt,
                    wt.Type,
                    wt.Amount,
                    CouponCode = wt.Coupon != null ? wt.Coupon.Code : null,
                    // إذا كان هناك اسم للعرض داخل جدول التحويلات، يمكنك إضافته هنا
                    // OfferName = wt.Conversion != null ? wt.Conversion.OfferName : null
                })
                .ToListAsync(cancellationToken);

            // بناء التفاصيل في الميموري
            var activities = pagedData.Select(wt => new ActivityItemDto(
                Timestamp: wt.CreatedAt,
                EventType: FormatEventType(wt.Type),
                Details: GenerateDetails(wt.Type, wt.CouponCode),
                PointsChange: wt.Amount,
                Status: "Completed" // لأن هذه حركات مالية تمت بالفعل في المحفظة
            )).ToList();

            return new Response(totalCount, activities);
        }

        // دالة مساعدة لتنسيق اسم الحدث ليتوافق مع الـ UI
        private static string FormatEventType(WalletTransactionType type) => type switch
        {
            WalletTransactionType.OfferReward => "OFFER COMPLETED",
            WalletTransactionType.CouponCreation => "REDEMPTION",
            WalletTransactionType.CouponCancellation => "REFUND",
            WalletTransactionType.Chargeback => "CHARGEBACK",
            WalletTransactionType.ManualAdjustment => "SUPPORT CREDIT",
            WalletTransactionType.Bonus => "BONUS",
            _ => type.ToString().ToUpper()
        };

        // دالة مساعدة لإنشاء النص الوصفي للعملية
        private static string GenerateDetails(WalletTransactionType type, string? couponCode) => type switch
        {
            WalletTransactionType.CouponCreation => !string.IsNullOrEmpty(couponCode) ? $"Redeemed Code {couponCode}" : "Redeemed points for a code",
            WalletTransactionType.CouponCancellation => !string.IsNullOrEmpty(couponCode) ? $"Refund for Code {couponCode}" : "Refunded cancelled code",
            WalletTransactionType.OfferReward => "Completed an offer", // يمكن استبدالها باسم العرض الفعلي إذا قمت بجلبه
            WalletTransactionType.Chargeback => "Points deducted due to chargeback",
            WalletTransactionType.ManualAdjustment => "Manual adjustment by Admin",
            WalletTransactionType.Bonus => "System bonus credit",
            _ => "System transaction"
        };
    }

    // 5. Endpoint
    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapGet("/admin/users/{userId:guid}/activity", async (
                Guid userId,
                [FromQuery] string? eventType,
                [FromQuery] DateTime? startDate,
                [FromQuery] DateTime? endDate,
                [FromQuery] int pageNumber,
                [FromQuery] int pageSize,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                pageNumber = pageNumber > 0 ? pageNumber : 1;
                pageSize = pageSize > 0 ? pageSize : 10;

                var query = new Query(userId, eventType, startDate, endDate, pageNumber, pageSize);
                var result = await sender.Send(query, cancellationToken);

                return result.ToHttpResponse();
            })
            .RequireAuthorization();
        }
    }
}
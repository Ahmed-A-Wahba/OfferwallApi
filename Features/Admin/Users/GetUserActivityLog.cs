using Carter;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OfferwallApi.Infrastructure.Authorization;
using OfferwallApi.Infrastructure.Messaging;
using OfferwallApi.Infrastructure.Persistence;
using OfferwallApi.Infrastructure.Results;
using OfferwallApi.Shared.Entities;

namespace OfferwallApi.Features.Admin.Users;

public static class GetUserActivityLog
{
    // 1. Query مع نقل [Authorize] هنا
    [Authorize(Role = "Admin")]
    public sealed record Query(
        Guid UserId,
        string? EventType = null, // "All Events", "OfferReward", "CouponCreation", etc.
        DateTime? StartDate = null,
        DateTime? EndDate = null,
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
    internal sealed class Handler(ApplicationDbContext dbContext)
        : IQueryHandler<Query, Result<Response>>
    {
        public async Task<Result<Response>> Handle(Query request, CancellationToken cancellationToken)
        {
            var query = dbContext.WalletTransactions
                .AsNoTracking()
                .Where(wt => wt.UserId == request.UserId);

            // 1. تطبيق فلاتر التاريخ
            if (request.StartDate.HasValue)
                query = query.Where(wt => wt.CreatedAt >= request.StartDate.Value);

            if (request.EndDate.HasValue)
                query = query.Where(wt => wt.CreatedAt <= request.EndDate.Value);

            // 2. تطبيق فلتر نوع الحدث
            if (!string.IsNullOrWhiteSpace(request.EventType) && !request.EventType.Equals("All Events", StringComparison.OrdinalIgnoreCase))
            {
                if (Enum.TryParse<WalletTransactionType>(request.EventType, true, out var parsedType))
                {
                    query = query.Where(wt => wt.Type == parsedType);
                }
            }

            int totalCount = await query.CountAsync(cancellationToken);

            // 3. اسقاط البيانات (Projection) وسحب تفاصيل الكوبون والعرض بأمان
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
                    OfferName = wt.Conversion != null && wt.Conversion.OfferCache != null
                        ? wt.Conversion.OfferCache.Name
                        : (wt.Conversion != null ? wt.Conversion.OfferId : null)
                })
                .ToListAsync(cancellationToken);

            // 4. بناء الـ DTOs والتفاصيل في الميموري
            var activities = pagedData.Select(wt => new ActivityItemDto(
                Timestamp: wt.CreatedAt,
                EventType: FormatEventType(wt.Type),
                Details: GenerateDetails(wt.Type, wt.CouponCode, wt.OfferName),
                PointsChange: wt.Amount,
                Status: "COMPLETED"
            )).ToList();

            return new Response(totalCount, activities);
        }

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

        private static string GenerateDetails(WalletTransactionType type, string? couponCode, string? offerName) => type switch
        {
            WalletTransactionType.CouponCreation => !string.IsNullOrEmpty(couponCode)
                ? $"Redeemed Code {couponCode}"
                : "Redeemed points for a gift code",
            WalletTransactionType.CouponCancellation => !string.IsNullOrEmpty(couponCode)
                ? $"Refund for Code {couponCode}"
                : "Refunded cancelled code",
            WalletTransactionType.OfferReward => !string.IsNullOrEmpty(offerName)
                ? $"Completed offer: {offerName}"
                : "Completed an offer",
            WalletTransactionType.Chargeback => "Points deducted due to chargeback",
            WalletTransactionType.ManualAdjustment => "Manual adjustment by Admin",
            WalletTransactionType.Bonus => "System bonus credit",
            _ => "System transaction"
        };
    }

    // 5. Endpoint Definition
    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapGet("/admin/users/{userId:guid}/activity", async (
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
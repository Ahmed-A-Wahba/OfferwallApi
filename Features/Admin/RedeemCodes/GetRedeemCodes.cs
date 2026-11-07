using Carter;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OfferwallApi.Infrastructure.Authorization;
using OfferwallApi.Infrastructure.Messaging;
using OfferwallApi.Infrastructure.Persistence;
using OfferwallApi.Infrastructure.Results;
using OfferwallApi.Shared.Entities;

namespace OfferwallApi.Features.Admin.RedeemCodes;

public static class GetRedeemCodes
{
    // 1. Query
    [Authorize(Role = "Admin")]
    public sealed record Query(
        string? SearchTerm = null,
        string? Status = null, // "All", "Active", "Used", "Expired", "Recalled"
        int PageNumber = 1,
        int PageSize = 10
    ) : IQuery<Result<Response>>;

    // 2. Response DTOs
    public sealed record Response(
        int TotalCount,
        List<RedeemCodeItemDto> Codes
    );

    public sealed record RedeemCodeItemDto(
        Guid Id,
        string Code,
        int Points,
        decimal Value,
        string UserId,
        string Status,
        DateTime CreatedAt,
        DateTime ExpiresAt
    );

    // 3. Validator
    internal sealed class Validator : AbstractValidator<Query>
    {
        public Validator()
        {
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
            var query = dbContext.Coupons.AsNoTracking();

            // 1. تطبيق الفلترة بالبحث
            if (!string.IsNullOrWhiteSpace(request.SearchTerm))
            {
                var term = request.SearchTerm.Trim();
                query = query.Where(x =>
                    EF.Functions.Like(x.Code, $"%{term}%") ||
                    EF.Functions.Like(x.UserId.ToString(), $"%{term}%"));
            }

            // 2. تطبيق فلترة الحالة (Status Filter)
            if (!string.IsNullOrWhiteSpace(request.Status) && !request.Status.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                if (Enum.TryParse<CouponStatus>(request.Status, true, out var parsedStatus))
                {
                    query = query.Where(x => x.Status == parsedStatus);
                }
            }

            // 3. حساب إجمالي العناصر للصفحة
            int totalCount = await query.CountAsync(cancellationToken);

            // 4. اسقاط البيانات مع الوقاية من NullReferenceException في علاقة الشريك
            var pagedData = await query
                .OrderByDescending(x => x.CreatedAt)
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(x => new
                {
                    x.CouponId,
                    x.Code,
                    x.Value,
                    x.UserId,
                    x.Status,
                    x.CreatedAt,
                    x.ExpiresAt,
                    PointsPerUsd = x.Partner != null ? x.Partner.PointsPerUsd : 1000 // افتراضي 1000 نقطة للدولار
                })
                .ToListAsync(cancellationToken);

            // 5. تحويل البيانات لـ DTO
            var codeItems = pagedData.Select(x =>
            {
                var userIdStr = x.UserId.ToString();
                var shortUserId = userIdStr.Length >= 5 ? userIdStr[..5].ToUpper() : userIdStr.ToUpper();

                return new RedeemCodeItemDto(
                    Id: x.CouponId,
                    Code: x.Code,
                    Points: (int)(x.Value * x.PointsPerUsd),
                    Value: x.Value,
                    UserId: $"#USR-{shortUserId}",
                    Status: x.Status.ToString().ToUpper(),
                    CreatedAt: x.CreatedAt,
                    ExpiresAt: x.ExpiresAt
                );
            }).ToList();

            return new Response(totalCount, codeItems);
        }
    }

    // 5. Endpoint
    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapGet("/admin/redeem-codes", async (
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
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

namespace OfferwallApi.Features.Admin.RedeemCodes;

public static class GetRedeemCodes
{
    // 1. Query
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
    [Authorize(Role = "Admin")]
    internal sealed class Handler(ApplicationDbContext dbContext)
        : IQueryHandler<Query, Result<Response>>
    {
        public async Task<Result<Response>> Handle(Query request, CancellationToken cancellationToken)
        {
            var query = dbContext.Coupons.AsNoTracking().AsQueryable();

            // Apply Search Filter
            if (!string.IsNullOrWhiteSpace(request.SearchTerm))
            {
                var searchTerm = request.SearchTerm.ToLower();
                query = query.Where(x =>
                    x.Code.ToLower().Contains(searchTerm) ||
                    x.UserId.ToString().ToLower().Contains(searchTerm));
            }

            // Apply Status Filter
            if (!string.IsNullOrWhiteSpace(request.Status) && !request.Status.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                if (Enum.TryParse<CouponStatus>(request.Status, true, out var parsedStatus))
                {
                    query = query.Where(x => x.Status == parsedStatus);
                }
            }

            // Get total count for pagination
            int totalCount = await query.CountAsync(cancellationToken);

            // Fetch required data only including PointsPerUsd from Partner
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
                    PointsPerUsd = x.Partner.PointsPerUsd // بنجيبها من جدول الشريك مباشرة هنا
                })
                .ToListAsync(cancellationToken);

            // Map to DTO in memory
            var codeItems = pagedData.Select(x => new RedeemCodeItemDto(
                Id: x.CouponId,
                Code: x.Code,
                Points: (int)(x.Value * x.PointsPerUsd), // الحساب بالاعتماد على قيمة البارتنر
                Value: x.Value,
                UserId: $"#USR-{x.UserId.ToString().Substring(0, 5).ToUpper()}",
                Status: x.Status.ToString().ToUpper(),
                CreatedAt: x.CreatedAt,
                ExpiresAt: x.ExpiresAt
            )).ToList();

            return new Response(totalCount, codeItems);
        }
    }

    // 5. Endpoint
    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapGet("/admin/redeem-codes", async (
                [FromQuery] string? searchTerm,
                [FromQuery] string? status,
                [FromQuery] int pageNumber,
                [FromQuery] int pageSize,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                pageNumber = pageNumber > 0 ? pageNumber : 1;
                pageSize = pageSize > 0 ? pageSize : 10;

                var query = new Query(searchTerm, status, pageNumber, pageSize);
                var result = await sender.Send(query, cancellationToken);

                return result.ToHttpResponse();
            })
            .RequireAuthorization();
        }
    }
}
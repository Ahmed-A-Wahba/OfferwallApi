using Carter;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfferwallApi.Infrastructure.Authorization;
using OfferwallApi.Infrastructure.Interfaces;
using OfferwallApi.Infrastructure.Persistence;
using OfferwallApi.Infrastructure.Results;
using OfferwallApi.Shared.Entities;

namespace OfferwallApi.Features.User.Codes;

public static class GetUserCoupons
{
    [Authorize(Role = "User")]
    public sealed record Query(CouponStatus? Status) : IRequest<Result<Response>>;

    public sealed record CouponDto(
        Guid CouponId,
        string Code,
        decimal PointsValue,
        decimal UsdValue,
        string CountryCode,
        string CityCode,
        string Status,
        DateTime CreatedAt,
        DateTime? RedeemedAt
    );

    public sealed record CouponHistorySummaryDto(
        decimal TotalRedeemedPoints,
        decimal TotalRedeemedUsd,
        int TotalRedemptions
    );

    public sealed record Response(
        CouponHistorySummaryDto Summary,
        List<CouponDto> Coupons
    );

    public sealed class Handler(ApplicationDbContext dbContext, ICurrentUser currentUser) : IRequestHandler<Query, Result<Response>>
    {

        public async Task<Result<Response>> Handle(Query request, CancellationToken cancellationToken)
        {
            var userId = currentUser.UserId;

            var userPartner = await dbContext.Users
                .AsNoTracking()
                .Where(u => u.UserId == userId)
                .Select(u => new { u.Partner.PointsPerUsd })
                .FirstOrDefaultAsync(cancellationToken);

            decimal pointsPerUsd = userPartner?.PointsPerUsd ?? 100m; // Default rate

            // الـ Query الأساسي
            var baseQuery = dbContext.Coupons
                .AsNoTracking()
                .Where(c => c.UserId == userId);

            // حساب الـ Summary
            var redeemedCoupons = await baseQuery
                .Where(c => c.Status == CouponStatus.Redeemed)
                .ToListAsync(cancellationToken);

            var totalPoints = redeemedCoupons.Sum(c => c.Value);
            var totalUsd = pointsPerUsd > 0 ? totalPoints / pointsPerUsd : 0m;

            var summary = new CouponHistorySummaryDto(
                TotalRedeemedPoints: totalPoints,
                TotalRedeemedUsd: Math.Round(totalUsd, 2),
                TotalRedemptions: redeemedCoupons.Count
            );

            var filteredQuery = baseQuery;
            if (request.Status.HasValue)
            {
                filteredQuery = filteredQuery.Where(c => c.Status != CouponStatus.Cancelled && c.Status == request.Status.Value);
            }

            var couponsList = await filteredQuery
                .OrderByDescending(c => c.CreatedAt)
                .Select(c => new CouponDto(
                    c.CouponId,
                    c.Code,
                    c.Value,
                    pointsPerUsd > 0 ? Math.Round(c.Value / pointsPerUsd, 2) : 0m,
                    c.CountryCode,
                    c.CityCode,
                    c.Status.ToString(),
                    c.CreatedAt,
                    c.RedeemedAt
                ))
                .ToListAsync(cancellationToken);

            var response = new Response(summary, couponsList);

            return response;
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapGet("/user/coupons", async (
                [FromQuery] CouponStatus? status,
                ISender sender = null!,
                CancellationToken cancellationToken = default) =>
            {
                var query = new Query(status);
                var result = await sender.Send(query, cancellationToken);

                return result.ToHttpResponse();
            }).RequireAuthorization();
        }
    }
}
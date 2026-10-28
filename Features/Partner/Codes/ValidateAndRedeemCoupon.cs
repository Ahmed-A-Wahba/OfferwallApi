using System.Net.Http.Headers;
using System.Text;
using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using OfferwallApi.Infrastructure.Persistence;
using OfferwallApi.Infrastructure.Results;
using OfferwallApi.Shared.Entities;

namespace OfferwallApi.Features.Partner.Codes;

public static class ValidateAndRedeemCoupon
{
    public sealed record Command(
        string ApiKey,
        string ApiSecret,
        string Code
    ) : IRequest<Result<Response>>;

    public sealed record Response(
        Guid CouponId,
        string Code,
        decimal UsdValue,
        Guid UserId,
        string CountryCode,
        string Status,
        DateTime CreatedAt,
        DateTime RedeemedAt
    );

    public sealed class Handler(ApplicationDbContext dbContext)
        : IRequestHandler<Command, Result<Response>>
    {
        public async Task<Result<Response>> Handle(Command request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Code))
            {
                return Error.Failure("Coupon.InvalidCode", "Coupon code is required.");
            }

            // 1. التحقق السيرفري العكسي من الـ ApiKey والـ ApiSecret معاً
            var partner = await dbContext.Partners
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.ApiKey == request.ApiKey && p.ApiSecret == request.ApiSecret, cancellationToken);

            if (partner is null)
            {
                return Error.Failure("Partner.Unauthorized", "Invalid Partner Credentials.");
            }

            var cleanCode = request.Code.Trim();

            // 2. البحث عن الكوبون للتأكد من ملكيته لمتجر هذا الشريك
            var coupon = await dbContext.Coupons
                .FirstOrDefaultAsync(c => c.Code == cleanCode && c.PartnerId == partner.PartnerId, cancellationToken);

            if (coupon is null)
            {
                return Error.Failure("Coupon.NotFound", "The coupon code is invalid or does not belong to your store.");
            }

            // 3. التحقق من حالات الكوبون
            if (coupon.Status == CouponStatus.Redeemed)
            {
                return Error.Failure("Coupon.AlreadyRedeemed", "This coupon has already been used.");
            }

            if (coupon.Status == CouponStatus.Cancelled)
            {
                return Error.Failure("Coupon.Cancelled", "This coupon has been cancelled.");
            }

            if (coupon.Status == CouponStatus.Expired || DateTime.UtcNow > coupon.ExpiresAt)
            {
                coupon.Status = CouponStatus.Expired;
                await dbContext.SaveChangesAsync(cancellationToken);
                return Error.Failure("Coupon.Expired", "This coupon has expired.");
            }

            var now = DateTime.UtcNow;
            var strategy = dbContext.Database.CreateExecutionStrategy();

            // 4. تنفيذ الاستبدال المضمون (Atomic Redeem)
            return await strategy.ExecuteAsync(async () =>
            {
                await using var dbTransaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

                try
                {
                    coupon.Status = CouponStatus.Redeemed;
                    coupon.UpdatedAt = now;

                    await dbContext.SaveChangesAsync(cancellationToken);
                    await dbTransaction.CommitAsync(cancellationToken);

                    return Result<Response>.Success(new Response(
                        coupon.CouponId,
                        coupon.Code,
                        coupon.Value, // القيمة الصافية بالدولار المسموح خصمها للزبون
                        coupon.UserId,
                        coupon.CountryCode,
                        coupon.Status.ToString(),
                        coupon.CreatedAt,
                        now
                    ));
                }
                catch
                {
                    await dbTransaction.RollbackAsync(cancellationToken);
                    throw;
                }
            });
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public sealed record RedeemRequest(string Code);

        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapPost("/api/partner/coupons/redeem", async (
                HttpContext context,
                [FromBody] RedeemRequest body,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                // استخراج الـ Authorization Header المعياري (Basic Auth)
                if (!context.Request.Headers.TryGetValue("Authorization", out var authHeader) ||
                    !AuthenticationHeaderValue.TryParse(authHeader, out var headerValue) ||
                    !string.Equals(headerValue.Scheme, "Basic", StringComparison.OrdinalIgnoreCase) ||
                    string.IsNullOrEmpty(headerValue.Parameter))
                {
                    return Results.Unauthorized();
                }

                string apiKey;
                string apiSecret;

                try
                {
                    var credentialsBytes = Convert.FromBase64String(headerValue.Parameter);
                    var credentials = Encoding.UTF8.GetString(credentialsBytes).Split(':', 2);

                    if (credentials.Length != 2)
                        return Results.Unauthorized();

                    apiKey = credentials[0];
                    apiSecret = credentials[1];
                }
                catch
                {
                    return Results.Unauthorized();
                }

                var command = new Command(apiKey, apiSecret, body.Code);
                var result = await sender.Send(command, cancellationToken);

                return result.ToHttpResponse();
            })
            .AllowAnonymous()
            .WithTags("Partner API");
        }
    }
}
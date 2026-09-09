using Carter;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfferwallApi.Infrastructure.Authorization;
using OfferwallApi.Infrastructure.Interfaces;
using OfferwallApi.Infrastructure.Persistence;
using OfferwallApi.Infrastructure.Results;
using OfferwallApi.Shared.Entities;

namespace OfferwallApi.Features.Partner.Codes;

public static class RedeemUserCode
{
    [Authorize(Role = "Partner")]
    public sealed record Command(string Code) : IRequest<Result<Response>>;

    public sealed record Response(string CountryCode, string CityCode);

    public sealed class Handler(
        ApplicationDbContext dbContext,
        ICurrentUser currentUser) : IRequestHandler<Command, Result<Response>>
    {
        public async Task<Result<Response>> Handle(Command request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Code))
            {
                return Error.Failure("InvalidCode", "Code cannot be empty.");
            }

            var partnerId = currentUser.UserId;

            var coupon = await dbContext.Coupons
                .FirstOrDefaultAsync(c => c.Code == request.Code && c.PartnerId == partnerId, cancellationToken);

            if (coupon is null)
            {
                return Error.NotFound("CodeNotFound", "The provided code is invalid for this partner.");
            }

            if (coupon.Status == CouponStatus.Redeemed)
            {
                return Error.Failure("CodeAlreadyRedeemed", "This code has already been redeemed.");
            }

            if (coupon.ExpiresAt < DateTime.UtcNow)
            {
                return Error.Failure("CodeExpired", "This code has expired.");
            }

            var now = DateTime.UtcNow;

            coupon.Status = CouponStatus.Redeemed;
            coupon.RedeemedAt = now;
            coupon.UpdatedAt = now;

            await dbContext.SaveChangesAsync(cancellationToken);

            return new Response(
                CountryCode: coupon.CountryCode,
                CityCode: coupon.CityCode
            );
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public sealed record RedeemCodeRequest(string Code);

        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapPost("/partner/codes/redeem", async (
                [FromBody] RedeemCodeRequest body,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var command = new Command(body.Code);
                var result = await sender.Send(command, cancellationToken);

                return result.ToHttpResponse();
            }).RequireAuthorization();
        }
    }
}
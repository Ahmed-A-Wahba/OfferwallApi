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

public static class RecallRedeemCode
{
    [Authorize(Role = "Admin")]
    public sealed record Command(Guid CouponId) : ICommand<Result>;

    // 2. Validator
    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.CouponId).NotEmpty();
        }
    }

    // 3. Handler
    internal sealed class Handler(ApplicationDbContext dbContext)
        : ICommandHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            // 1. استعلام خفيف على الكوبون فقط بدون Include للشركاء
            var coupon = await dbContext.Coupons
                .FirstOrDefaultAsync(c => c.CouponId == request.CouponId, cancellationToken);

            if (coupon is null)
                return Error.NotFound("Coupon.NotFound", "The specified redeem code was not found.");

            // 2. التأكد من أن الكود في حالة Active فقط لإلغائه
            if (coupon.Status != CouponStatus.Active)
                return Error.Validation(
                    "Coupon.CannotRecall",
                    $"Cannot recall a code with status '{coupon.Status}'. Only ACTIVE codes can be recalled.");

            coupon.Status = CouponStatus.Cancelled;
            coupon.UpdatedAt = DateTime.UtcNow;

            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
    }

    // 4. Endpoint Definition
    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapPost("/admin/redeem-codes/{id:guid}/recall", async (
                Guid id,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var command = new Command(id);
                var result = await sender.Send(command, cancellationToken);

                return result.ToHttpResponse();
            });
        }
    }
}
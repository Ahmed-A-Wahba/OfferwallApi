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
    // 1. Command
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
    [Authorize(Role = "Admin")]
    internal sealed class Handler(ApplicationDbContext dbContext)
        : ICommandHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            var coupon = await dbContext.Coupons
                .Include(c => c.Partner)
                .FirstOrDefaultAsync(c => c.CouponId == request.CouponId, cancellationToken);

            if (coupon is null)
                return Error.NotFound("Coupon.NotFound", "The specified redeem code was not found.");

            // التأكد من أن الكود بحالة Active فقط لإلغائه
            if (coupon.Status != CouponStatus.Active)
                return Error.Validation("Coupon.CannotRecall", $"Cannot recall a code with status '{coupon.Status}'. Only ACTIVE codes can be recalled.");

            // 1. تحديث حالة الكوبون إلى Recalled
            coupon.Status = CouponStatus.Cancelled; // أو CouponStatus.Recalled إذا كان لديك حالة Recalled
            coupon.UpdatedAt = DateTime.UtcNow;

            // 2. إضافة المعاملة للـ WalletTransactions إذا أردت تسجيل عملية الاسترجاع للرموز/النقاط
            /*
            var pointsToRefund = (int)(coupon.Value * coupon.Partner.PointsPerUsd);
            var refundTransaction = new WalletTransaction
            {
                WalletTransactionId = Guid.NewGuid(),
                UserId = coupon.UserId,
                CouponId = coupon.CouponId,
                Amount = pointsToRefund,
                Type = TransactionType.Refund, // أو النوع المناسب لديك
                CreatedAt = DateTime.UtcNow
            };
            dbContext.WalletTransactions.Add(refundTransaction);
            */

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
            })
            .RequireAuthorization();
        }
    }
}
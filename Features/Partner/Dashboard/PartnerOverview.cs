using Carter;
using MediatR;
using OfferwallApi.Entities;
using OfferwallApi.Infrastructure.Interfaces;
using OfferwallApi.Infrastructure.Messaging;
using OfferwallApi.Infrastructure.Persistence;
using OfferwallApi.Infrastructure.Results;
using Microsoft.EntityFrameworkCore;
using OfferwallApi.Infrastructure.Authorization;
using OfferwallApi.Shared.Entities;

namespace OfferwallApi.Features.Partner.Dashboard;

public static class PartnerOverview
{
    public sealed record Query : IQuery<Result<Response>>;

    public sealed record Response(
        string CompanyName,
        string? LogoUrl,
        int CouponsGenerated,
        int CouponsRedeemed,
        decimal TotalPaid,
        decimal TotalInThisMonth,
        decimal PendingSettlements,
        decimal Chargebacks
    );

    [Authorize(Role = "Partner")]
    internal sealed class Handler(
    ApplicationDbContext dbContext,
    ICurrentUser currentUser)
    : IQueryHandler<Query, Result<Response>>
    {
        public async Task<Result<Response>> Handle(
            Query request,
            CancellationToken cancellationToken)
        {
            var partnerId = currentUser.UserId;

            var partner = await dbContext.Partners
                .FirstOrDefaultAsync(
                    x => x.PartnerId == partnerId,
                    cancellationToken);

            var couponsGenerated = await dbContext.Coupons
                .CountAsync(
                    x => x.PartnerId == partnerId,
                    cancellationToken);

            var couponsRedeemed = await dbContext.Coupons
                .CountAsync(
                    x => x.PartnerId == partnerId &&
                         x.Status == CouponStatus.Redeemed,
                    cancellationToken);

            var totalPaid = await dbContext.Payouts
                .Where(x =>
                    x.PartnerId == partnerId &&
                    x.Status == PayoutStatus.Paid)
                .SumAsync(
                    x => (decimal?)x.Amount,
                    cancellationToken) ?? 0;

            var monthStart = new DateTime(
                DateTime.UtcNow.Year,
                DateTime.UtcNow.Month,
                1);

            var totalInThisMonth = await dbContext.WalletTransactions
                .Where(x =>
                    x.User.PartnerId == partnerId &&
                    x.Type == WalletTransactionType.CouponRedeem &&
                    x.CreatedAt >= monthStart)
                .SumAsync(
                    x => (decimal?)x.Amount,
                    cancellationToken) ?? 0;

            var pendingSettlements = await dbContext.Payouts
                .Where(x =>
                    x.PartnerId == partnerId &&
                    x.Status == PayoutStatus.Pending)
                .SumAsync(
                    x => (decimal?)x.Amount,
                    cancellationToken) ?? 0;

            decimal chargebacks = 0m;

            if (partner!.HasChargebacks)
            {
                chargebacks = await dbContext.Conversions
                    .Where(x =>
                        x.User.PartnerId == partnerId &&
                        x.Type == ConversionType.Chargeback)
                    .SumAsync(
                        x => (decimal?)x.Payout,
                        cancellationToken) ?? 0;
            }

            return new Response(
                partner.CompanyName,
                partner.LogoUrl,
                couponsGenerated,
                couponsRedeemed,
                totalPaid,
                totalInThisMonth,
                pendingSettlements,
                chargebacks);
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapGet(
                "/partners/dashboard/overview",
                async (ISender sender) =>
                {
                    var result = await sender.Send(new Query());

                    return result.ToHttpResponse();
                })
                .RequireAuthorization();
        }
    }
}

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

public static class GetUserProfile
{
    // 1. Query مع نقل حارس الأمان هنا
    [Authorize(Role = "Admin")]
    public sealed record Query(string SearchTerm) : IQuery<Result<Response>>;

    // 2. Response DTOs
    public sealed record Response(
        UserProfileDto User,
        UserStatsDto Stats
    );

    public sealed record UserProfileDto(
        Guid Id,
        string DisplayId,
        string PartnerName,
        string Status,
        decimal CurrentBalance,
        string PublisherUserId
    );

    public sealed record UserStatsDto(
        int TotalOffersCompleted,
        decimal TotalPointsEarned,
        decimal TotalRedemptions
    );

    // 3. Validator
    internal sealed class Validator : AbstractValidator<Query>
    {
        public Validator()
        {
            RuleFor(x => x.SearchTerm).NotEmpty().MinimumLength(3);
        }
    }

    // 4. Handler
    internal sealed class Handler(ApplicationDbContext dbContext)
        : IQueryHandler<Query, Result<Response>>
    {
        public async Task<Result<Response>> Handle(Query request, CancellationToken cancellationToken)
        {
            var term = request.SearchTerm.Trim();
            bool isGuid = Guid.TryParse(term, out var searchGuid);

            // 1. البحث عن المستخدم واسقاط البيانات بـ Projection خفيف
            var user = await dbContext.Users
                .AsNoTracking()
                .Where(u => (isGuid && u.UserId == searchGuid) || EF.Functions.Like(u.ExternalUserId, term))
                .Select(u => new
                {
                    u.UserId,
                    u.ExternalUserId,
                    PartnerName = u.Partner != null ? u.Partner.CompanyName : "UNKNOWN",
                    CurrentBalance = u.Wallet != null ? u.Wallet.Balance : 0m
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (user is null)
                return Error.NotFound("User.NotFound", "No user found matching the search term.");

            // 2. حساب الإحصائيات (Stats) بكفاءة مع حماية من الـ Null
            var totalOffers = await dbContext.Conversions
                .AsNoTracking()
                .CountAsync(c => c.UserId == user.UserId && c.Type == ConversionType.Conversion, cancellationToken);

            var totalEarned = await dbContext.WalletTransactions
                .AsNoTracking()
                .Where(wt => wt.UserId == user.UserId && wt.Amount > 0)
                .SumAsync(wt => (decimal?)wt.Amount, cancellationToken) ?? 0m;

            var totalRedemptions = await dbContext.Coupons
                .AsNoTracking()
                .CountAsync(c => c.UserId == user.UserId, cancellationToken);

            // 3. تشكيل الاستجابة
            var userIdStr = user.UserId.ToString();
            var shortDisplayId = $"#USR-{userIdStr[..5].ToUpper()}";

            var response = new Response(
                User: new UserProfileDto(
                    Id: user.UserId,
                    DisplayId: shortDisplayId,
                    PartnerName: user.PartnerName.ToUpper(),
                    Status: "ACTIVE",
                    CurrentBalance: user.CurrentBalance,
                    PublisherUserId: user.ExternalUserId
                ),
                Stats: new UserStatsDto(
                    TotalOffersCompleted: totalOffers,
                    TotalPointsEarned: totalEarned,
                    TotalRedemptions: totalRedemptions
                )
            );

            return response;
        }
    }

    // 5. Endpoint Definition
    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapGet("/admin/users/profile", async (
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
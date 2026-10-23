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

namespace OfferwallApi.Features.Admin.Users;

public static class GetUserProfile
{
    // 1. Query
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
    [Authorize(Role = "Admin")]
    internal sealed class Handler(ApplicationDbContext dbContext)
        : IQueryHandler<Query, Result<Response>>
    {
        public async Task<Result<Response>> Handle(Query request, CancellationToken cancellationToken)
        {
            var searchTerm = request.SearchTerm.ToLower().Trim();
            bool isGuid = Guid.TryParse(searchTerm, out var searchGuid);

            // 1. البحث عن المستخدم (بالـ ID أو ExternalUserId)
            var user = await dbContext.Users
                .Include(u => u.Partner)
                .Include(u => u.Wallet) // لجلب الرصيد
                .AsNoTracking()
                .FirstOrDefaultAsync(u =>
                    (isGuid && u.UserId == searchGuid) ||
                    u.ExternalUserId.ToLower() == searchTerm,
                    cancellationToken);

            if (user is null)
                return Error.NotFound("User.NotFound", "No user found matching the search term.");

            // 2. حساب الإحصائيات (Stats)
            var totalOffers = await dbContext.Conversions
                .CountAsync(c => c.UserId == user.UserId && c.Type == ConversionType.Conversion, cancellationToken);

            var totalEarned = await dbContext.WalletTransactions
                .Where(wt => wt.UserId == user.UserId && wt.Amount > 0) // افتراض أن Amount يمثل النقاط المكتسبة
                .SumAsync(wt => wt.Amount, cancellationToken);

            var totalRedemptions = await dbContext.Coupons
                .CountAsync(c => c.UserId == user.UserId, cancellationToken);

            // 3. تشكيل الاستجابة
            var response = new Response(
                User: new UserProfileDto(
                    Id: user.UserId,
                    DisplayId: user.UserId.ToString(), // عرض الـ ID كما هو في الداتابيز
                    PartnerName: user.Partner?.CompanyName?.ToUpper() ?? "UNKNOWN",
                    Status: "ACTIVE", // ثابتة مؤقتاً لعدم وجود حقل حالة في الـ Entity
                    CurrentBalance: user.Wallet?.Balance ?? 0,
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

    // 5. Endpoint
    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapGet("/admin/users/profile", async (
                [FromQuery] string searchTerm,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var query = new Query(searchTerm);
                var result = await sender.Send(query, cancellationToken);

                return result.ToHttpResponse();
            })
            .RequireAuthorization();
        }
    }
}
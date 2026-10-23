using Carter;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfferwallApi.Infrastructure.Interfaces;
using OfferwallApi.Infrastructure.Messaging;
using OfferwallApi.Infrastructure.Persistence;
using OfferwallApi.Infrastructure.Results;
using OfferwallApi.Shared.Entities;

namespace OfferwallApi.Features.User.Dashboard;

public static class InitUserDashboard
{
    public sealed record Query(
        Guid PubId,
        string ExternalUserId,
        string RequestOrigin
    ) : IQuery<Result<Response>>;

    public sealed record Response(
        decimal PointsBalance,
        string PublisherName,
        string? PublisherLogoUrl,
        int UserXp,
        string AccessToken,
        string RefreshToken,
        int ExpiresIn
    );

    internal sealed class Validator : AbstractValidator<Query>
    {
        public Validator()
        {
            RuleFor(x => x.PubId)
                .NotEmpty();

            RuleFor(x => x.ExternalUserId)
                .NotEmpty()
                .MaximumLength(100);

            RuleFor(x => x.RequestOrigin)
                .NotEmpty();
        }
    }

    public sealed class Handler(
        ApplicationDbContext dbContext,
        IJwtService jwtService,
        IJwtClaimsFactory claimsFactory)
        : IQueryHandler<Query, Result<Response>>
    {
        public async Task<Result<Response>> Handle(Query request, CancellationToken cancellationToken)
        {
            var partner = await dbContext.Partners
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.PartnerId == request.PubId, cancellationToken);

            if (partner is null
                || partner.Status != PublisherStatus.Active
                || !partner.IsEmailVerified
                || !IsOriginAllowed(request.RequestOrigin, partner.AllowedIframeOrigin))
            {
                return Error.Unauthorized(
                    code: "Partner.InvalidCredentials",
                    description: "Invalid partner credentials or unauthorized origin."
                );
            }

            var user = await dbContext.Users
                .Include(u => u.Wallet)
                .FirstOrDefaultAsync(
                    u => u.PartnerId == partner.PartnerId && u.ExternalUserId == request.ExternalUserId,
                    cancellationToken);

            var now = DateTime.UtcNow;

            if (user is null)
            {
                var userId = Guid.CreateVersion7();
                user = new Shared.Entities.User
                {
                    UserId = userId,
                    PartnerId = partner.PartnerId,
                    ExternalUserId = request.ExternalUserId,
                    Xp = 0,
                    CreatedAt = now,
                    UpdatedAt = now,
                    Wallet = new Wallet
                    {
                        UserId = userId,
                        Balance = 0,
                        CreatedAt = now,
                        UpdatedAt = now
                    }
                };

                dbContext.Users.Add(user);
            }

            var claims = claimsFactory.Create(user);
            var accessToken = jwtService.GenerateAccessToken(claims);
            var refreshTokenValue = jwtService.GenerateRefreshToken();

            // E. Save User Refresh Token
            var userRefreshToken = new UserRefreshToken
            {
                RefreshTokenId = Guid.CreateVersion7(),
                UserId = user.UserId,
                Token = refreshTokenValue,
                ExpiresAt = now.AddDays(30),
                CreatedAt = now,
            };

            dbContext.UserRefreshTokens.Add(userRefreshToken);

            await dbContext.SaveChangesAsync(cancellationToken);

            var balance = user.Wallet?.Balance ?? 0;

            return new Response(
                PointsBalance: balance * partner.PointsPerUsd,
                PublisherName: partner.CompanyName,
                PublisherLogoUrl: partner.LogoUrl,
                UserXp: user.Xp,
                AccessToken: accessToken,
                RefreshToken: refreshTokenValue,
                ExpiresIn: jwtService.ExpiresIn
            );
        }

        private static bool IsOriginAllowed(string requestOrigin, string? allowedOrigin)
        {
            if (string.IsNullOrWhiteSpace(allowedOrigin))
                return false;

            var cleanedRequestOrigin = requestOrigin.TrimEnd('/').ToLowerInvariant();
            var cleanedAllowedOrigin = allowedOrigin.TrimEnd('/').ToLowerInvariant();

            return cleanedRequestOrigin.Equals(cleanedAllowedOrigin, StringComparison.OrdinalIgnoreCase);
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapGet(
                "/user/wall/init",
                async (
                    [FromQuery] Guid pub_id,
                    [FromQuery] string user_id,
                    HttpContext httpContext,
                    ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    var origin = httpContext.Request.Headers.Origin.FirstOrDefault()
                                 ?? httpContext.Request.Headers.Referer.FirstOrDefault()
                                 ?? string.Empty;

                    var query = new Query(pub_id, user_id, origin);
                    var result = await sender.Send(query, cancellationToken);

                    return result.ToHttpResponse();
                });
        }
    }
}
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Carter;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OfferwallApi.Infrastructure.Authorization;
using OfferwallApi.Infrastructure.Interfaces;
using OfferwallApi.Infrastructure.Options;
using OfferwallApi.Infrastructure.Persistence;
using OfferwallApi.Infrastructure.Results;
using OfferwallApi.Shared.Entities;

namespace OfferwallApi.Features.User.Offers;

public static class StartOffer
{
    [Authorize(Role = "User")]
    public sealed record Command(
        string OfferId,
        string Name,
        string LogoUrl,
        decimal TotalReward,
        int TotalTasks) : IRequest<Result<Response>>;

    public sealed record Response(string RedirectUrl);

    public sealed class Handler(
        ApplicationDbContext dbContext,
        ICurrentUser currentUser,
        IOptions<KlinkOptions> klinkOptions) : IRequestHandler<Command, Result<Response>>
    {
        public async Task<Result<Response>> Handle(Command request, CancellationToken cancellationToken)
        {
            var userId = currentUser.UserId;

            var offerExists = await dbContext.OfferCaches
                .AnyAsync(o => o.OfferId == request.OfferId, cancellationToken);

            if (!offerExists)
            {
                dbContext.OfferCaches.Add(new OfferCache
                {
                    OfferId = request.OfferId,
                    Name = request.Name,
                    LogoUrl = request.LogoUrl,
                    TotalReward = request.TotalReward,
                    TotalTasks = request.TotalTasks,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                });
            }

            var click = new Click
            {
                ClickId = Guid.NewGuid(),
                UserId = userId,
                OfferId = request.OfferId,
                Status = ClickStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };

            dbContext.Clicks.Add(click);
            await dbContext.SaveChangesAsync(cancellationToken);

            var publisherId = klinkOptions.Value.PubId;
            var jwtSecret = klinkOptions.Value.ApiSecret;

            if (string.IsNullOrEmpty(publisherId) || string.IsNullOrEmpty(jwtSecret))
            {
                return Error.Failure("KlinkConfiguration", "Klink configuration is missing or invalid.");
            }

            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim("offerId", request.OfferId),
                new Claim("sub", userId.ToString()),
                new Claim("pub", publisherId),
                new Claim("k1", click.ClickId.ToString())
            };

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddHours(2),
                SigningCredentials = credentials
            };

            var tokenHandler = new JwtSecurityTokenHandler();

            tokenHandler.OutboundClaimTypeMap.Clear();

            var token = tokenHandler.CreateToken(tokenDescriptor);
            var jwtString = tokenHandler.WriteToken(token);

            var redirectUrl = $"https://app.klinkfinance.com/quest-pn?ot={jwtString}";

            return new Response(redirectUrl);
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public sealed record StartOfferRequest(
            string Name,
            string LogoUrl,
            decimal TotalReward,
            int TotalTasks);

        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapPost("/user/offers/{offerId}/start", async (
                string offerId,
                [FromBody] StartOfferRequest body,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var command = new Command(
                    offerId,
                    body.Name,
                    body.LogoUrl,
                    body.TotalReward,
                    body.TotalTasks);

                var result = await sender.Send(command, cancellationToken);

                return result.ToHttpResponse();
            }).RequireAuthorization();
        }
    }
}
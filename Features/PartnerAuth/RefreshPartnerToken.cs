using Carter;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OfferwallApi.Entities;
using OfferwallApi.Infrastructure.Interfaces;
using OfferwallApi.Infrastructure.Messaging;
using OfferwallApi.Infrastructure.Persistence;
using OfferwallApi.Infrastructure.Results;
using OfferwallApi.Shared.Errors;

namespace OfferwallApi.Features.PartnerAuth;

public static class RefreshPartnerToken
{
    public sealed record Command(
        string RefreshToken
    ) : ICommand<Result<Response>>;

    public sealed record Response(
        string AccessToken,
        string RefreshToken,
        int ExpiresIn
    );

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.RefreshToken)
                .NotEmpty();
        }
    }

    internal sealed class Handler(
    ApplicationDbContext dbContext,
    IJwtService jwtService,
    IJwtClaimsFactory claimsFactory)
    : ICommandHandler<Command, Result<Response>>
    {
        public async Task<Result<Response>> Handle(
            Command request,
            CancellationToken cancellationToken)
        {
            var refreshToken = await dbContext.PartnerRefreshTokens
                .Include(x => x.Partner)
                .FirstOrDefaultAsync(
                    x => x.Token == request.RefreshToken,
                    cancellationToken);

            if (refreshToken is null)
                return PartnerErrors.InvalidRefreshToken;

            if (refreshToken.RevokedAt is not null)
                return PartnerErrors.InvalidRefreshToken;

            if (refreshToken.ExpiresAt <= DateTime.UtcNow)
                return PartnerErrors.InvalidRefreshToken;

            var partner = refreshToken.Partner;

            if (!partner.IsEmailVerified)
                return PartnerErrors.EmailNotVerified;

            if (!partner.IsActive)
                return PartnerErrors.AccountPendingApproval;

            refreshToken.RevokedAt = DateTime.UtcNow;

            var newRefreshToken = jwtService.GenerateRefreshToken();

            dbContext.PartnerRefreshTokens.Add(new PartnerRefreshToken
            {
                RefreshTokenId = Guid.CreateVersion7(),
                PartnerId = partner.PartnerId,
                Token = newRefreshToken,
                ExpiresAt = DateTime.UtcNow.AddDays(30),
                CreatedAt = DateTime.UtcNow
            });

            var accessToken = jwtService.GenerateAccessToken(
                claimsFactory.Create(partner));

            await dbContext.SaveChangesAsync(cancellationToken);

            return new Response(
                accessToken,
                newRefreshToken,
                jwtService.ExpiresIn);
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapPost(
                "/partner/auth/refresh-token",
                async (Command command, ISender sender) =>
                {
                    var result = await sender.Send(command);
                    return result.ToHttpResponse();
                });
        }
    }
}
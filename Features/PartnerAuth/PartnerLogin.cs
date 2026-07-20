using FluentValidation;
using OfferwallApi.Infrastructure.Interfaces;
using OfferwallApi.Infrastructure.Messaging;
using OfferwallApi.Infrastructure.Persistence;
using OfferwallApi.Infrastructure.Results;
using Microsoft.EntityFrameworkCore;
using OfferwallApi.Shared.Errors;
using OfferwallApi.Entities;
using Carter;
using MediatR;

namespace OfferwallApi.Features.PartnerAuth;

public static class PartnerLogin
{
    public sealed record Command(
        string Email,
        string Password
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
            RuleFor(x => x.Email)
                .NotEmpty()
                .EmailAddress();

            RuleFor(x => x.Password)
    .NotEmpty();
        }
    }

    public sealed class Handler(
        ApplicationDbContext dbContext,
        IPasswordHasher passwordHasher,
        IJwtService jwtService,
        IJwtClaimsFactory claimsFactory
    ) : ICommandHandler<Command, Result<Response>>
    {
        public async Task<Result<Response>> Handle(Command request, CancellationToken cancellationToken)
        {
            var partner = await dbContext.Partners
                .FirstOrDefaultAsync(
                    x => x.Email == request.Email,
                    cancellationToken);

            if (partner is null)
                return PartnerErrors.InvalidCredentials;

            if (!passwordHasher.Verify(request.Password, partner.PasswordHash))
                return PartnerErrors.InvalidCredentials;

            if (!partner.IsEmailVerified)
                return PartnerErrors.EmailNotVerified;

            if (!partner.IsActive)
                return PartnerErrors.AccountPendingApproval;

            var accessToken = jwtService.GenerateAccessToken(claimsFactory.Create(partner));

            var refreshToken = jwtService.GenerateRefreshToken();

            dbContext.PartnerRefreshTokens.Add(
                new PartnerRefreshToken
                {
                    RefreshTokenId = Guid.CreateVersion7(),
                    PartnerId = partner.PartnerId,
                    Token = refreshToken,
                    ExpiresAt = DateTime.UtcNow.AddDays(30),
                    CreatedAt = DateTime.UtcNow
                });

            await dbContext.SaveChangesAsync(cancellationToken);

            return new Response(
                accessToken,
                refreshToken,
                jwtService.ExpiresIn);
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapPost(
                "/partner/auth/login",
                async (Command command, ISender sender) =>
                {
                    var result = await sender.Send(command);
                    return result.ToHttpResponse();
                });
        }
    }
}
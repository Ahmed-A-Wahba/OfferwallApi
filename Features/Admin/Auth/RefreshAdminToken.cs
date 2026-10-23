using Carter;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OfferwallApi.Infrastructure.Interfaces;
using OfferwallApi.Infrastructure.Messaging;
using OfferwallApi.Infrastructure.Persistence;
using OfferwallApi.Infrastructure.Results;
using OfferwallApi.Shared.Entities;
using OfferwallApi.Shared.Errors;

namespace OfferwallApi.Features.Admin.Auth;

public static class RefreshAdminToken
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
            var refreshToken = await dbContext.AdminRefreshTokens
                .Include(x => x.Admin)
                .FirstOrDefaultAsync(
                    x => x.Token == request.RefreshToken,
                    cancellationToken);

            if (refreshToken is null)
                return AdminErrors.InvalidRefreshToken;

            if (refreshToken.ExpiresAt <= DateTime.UtcNow)
                return AdminErrors.InvalidRefreshToken;

            var admin = refreshToken.Admin;

            dbContext.AdminRefreshTokens.Remove(refreshToken);

            var newRefreshToken = jwtService.GenerateRefreshToken();

            dbContext.AdminRefreshTokens.Add(new AdminRefreshToken
            {
                RefreshTokenId = Guid.CreateVersion7(),
                AdminId = admin.AdminId,
                Token = newRefreshToken,
                ExpiresAt = DateTime.UtcNow.AddDays(30),
                CreatedAt = DateTime.UtcNow
            });

            var accessToken = jwtService.GenerateAccessToken(
                claimsFactory.Create(admin));

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
                "/admin/auth/refresh-token",
                async (Command command, ISender sender) =>
                {
                    var result = await sender.Send(command);
                    return result.ToHttpResponse();
                }).AllowAnonymous();
        }
    }
}
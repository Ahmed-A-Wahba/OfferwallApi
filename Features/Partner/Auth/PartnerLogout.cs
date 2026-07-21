using FluentValidation;
using OfferwallApi.Infrastructure.Authorization;
using OfferwallApi.Infrastructure.Messaging;
using OfferwallApi.Infrastructure.Persistence;
using OfferwallApi.Infrastructure.Results;
using Microsoft.EntityFrameworkCore;
using Carter;
using MediatR;

namespace OfferwallApi.Features.Partner.Auth;

public static class PartnerLogout
{
    public sealed record Command(
        string RefreshToken
    ) : ICommand<Result>;

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.RefreshToken)
                .NotEmpty();
        }
    }

    [Authorize(Role = "Partner")]
    internal sealed class Handler(
    ApplicationDbContext dbContext)
    : ICommandHandler<Command, Result>
    {
        public async Task<Result> Handle(
            Command request,
            CancellationToken cancellationToken)
        {
            var refreshToken = await dbContext.PartnerRefreshTokens
                .FirstOrDefaultAsync(
                    x => x.Token == request.RefreshToken,
                    cancellationToken);

            if (refreshToken is null)
                return Result.Success();

            dbContext.PartnerRefreshTokens.Remove(refreshToken);

            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapPost(
                "/partners/logout",
                async (Command command, ISender sender) =>
                {
                    var result = await sender.Send(command);

                    return result.ToHttpResponse();
                })
                .RequireAuthorization();
        }
    }
}
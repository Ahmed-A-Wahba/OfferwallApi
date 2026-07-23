using Carter;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OfferwallApi.Infrastructure.Authorization;
using OfferwallApi.Infrastructure.Interfaces;
using OfferwallApi.Infrastructure.Messaging;
using OfferwallApi.Infrastructure.Persistence;
using OfferwallApi.Infrastructure.Results;

namespace OfferwallApi.Features.Partner.Integration;

public static class UpdatePointsRate
{
    [Authorize(Role = "Partner")]
    public sealed record Command(
        int PointsPerUsd
    ) : ICommand<Result>;

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.PointsPerUsd)
                .GreaterThan(0);

            RuleFor(x => x.PointsPerUsd)
                .LessThanOrEqualTo(100000);
        }
    }

    internal sealed class Handler(
    ApplicationDbContext dbContext,
    ICurrentUser currentUser)
    : ICommandHandler<Command, Result>
    {
        public async Task<Result> Handle(
            Command request,
            CancellationToken cancellationToken)
        {
            var partner = await dbContext.Partners
                .FirstOrDefaultAsync(
                    x => x.PartnerId == currentUser.UserId,
                    cancellationToken);

            if (partner is null)
            {
                return Error.NotFound(
                    "Partner.NotFound",
                    "Partner was not found.");
            }

            partner.PointsPerUsd = request.PointsPerUsd;

            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapPut(
                "/partners/integration/points-rate",
                async (
                    Command command,
                    ISender sender) =>
                {
                    var result = await sender.Send(command);

                    return result.ToHttpResponse();
                });
        }
    }
}
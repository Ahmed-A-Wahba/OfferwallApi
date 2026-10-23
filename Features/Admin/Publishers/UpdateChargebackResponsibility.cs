using Carter;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OfferwallApi.Infrastructure.Authorization;
using OfferwallApi.Infrastructure.Messaging;
using OfferwallApi.Infrastructure.Persistence;
using OfferwallApi.Infrastructure.Results;

namespace OfferwallApi.Features.Admin.Publishers;

public static class UpdateChargebackResponsibility
{
    public sealed record Command(
        Guid PublisherId,
        bool HasChargebacks
    ) : ICommand<Result>;

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.PublisherId).NotEmpty();
        }
    }

    [Authorize(Role = "Admin")]
    internal sealed class Handler(ApplicationDbContext dbContext)
        : ICommandHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            var partner = await dbContext.Partners
                .FirstOrDefaultAsync(p => p.PartnerId == request.PublisherId, cancellationToken);

            if (partner is null)
                return Error.NotFound("Publisher.NotFound", "Publisher not found.");

            partner.HasChargebacks = request.HasChargebacks;
            partner.UpdatedAt = DateTime.UtcNow;

            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapPut("/admin/publishers/{id:guid}/chargeback-responsibility", async (
                Guid id,
                Command body,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var command = body with { PublisherId = id };
                var result = await sender.Send(command, cancellationToken);
                return result.ToHttpResponse();
            })
            .RequireAuthorization();
        }
    }
}
using Carter;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OfferwallApi.Infrastructure.Authorization;
using OfferwallApi.Infrastructure.Messaging;
using OfferwallApi.Infrastructure.Persistence;
using OfferwallApi.Infrastructure.Results;
using OfferwallApi.Shared.Entities;

namespace OfferwallApi.Features.Admin.Publishers;

public static class ApprovePublisher
{
    public sealed record Command(Guid PublisherId) : ICommand<Result>;

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
            var publisher = await dbContext.Partners
                .FirstOrDefaultAsync(p => p.PartnerId == request.PublisherId, cancellationToken);

            if (publisher is null)
                return Error.NotFound("Publisher.NotFound", "Publisher not found.");

            if (publisher.Status == PublisherStatus.Active)
                return Result.Success();

            publisher.Status = PublisherStatus.Active;

            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapPost("/admin/publishers/{id:guid}/approve", async (
                Guid id,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var command = new Command(id);
                var result = await sender.Send(command, cancellationToken);
                return result.ToHttpResponse();
            })
            .RequireAuthorization();
        }
    }
}
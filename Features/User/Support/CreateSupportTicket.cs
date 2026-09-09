using Carter;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using OfferwallApi.Infrastructure.Authorization;
using OfferwallApi.Infrastructure.Interfaces;
using OfferwallApi.Infrastructure.Persistence;
using OfferwallApi.Infrastructure.Results;
using OfferwallApi.Shared.Entities;

namespace OfferwallApi.Features.User.Support;

public static class CreateSupportTicket
{
    [Authorize(Role = "User")]
    public sealed record Command(
        string Email,
        TicketCategory Category,
        string Subject,
        string Description,
        IFormFile? Screenshot
    ) : IRequest<Result<Response>>;

    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.Email)
                .NotEmpty()
                .EmailAddress()
                .WithMessage("A valid email address is required.");

            RuleFor(x => x.Category)
                .IsInEnum()
                .WithMessage("A valid category is required.");

            RuleFor(x => x.Subject)
                .NotEmpty()
                .WithMessage("Subject is required.");

            RuleFor(x => x.Description)
                .NotEmpty()
                .WithMessage("Description is required.");
        }
    }

    public sealed record Response(
        Guid TicketId,
        string Status,
        DateTime CreatedAt
    );


    public sealed class Handler(
        ApplicationDbContext dbContext,
        ICurrentUser currentUser,
        IStorageService storageService) : IRequestHandler<Command, Result<Response>>
    {
        public async Task<Result<Response>> Handle(Command request, CancellationToken cancellationToken)
        {
            var userId = currentUser.UserId;

            string? screenshotUrl = null;
            if (request.Screenshot is not null)
            {
                screenshotUrl = await storageService.SaveFileAsync(request.Screenshot, "support-tickets", cancellationToken);
            }

            var ticket = new SupportTicket
            {
                TicketId = Guid.CreateVersion7(),
                UserId = userId,
                Category = request.Category,
                Subject = request.Subject,
                Description = request.Description,
                ScreenshotUrl = screenshotUrl,
                Status = TicketStatus.Open,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            dbContext.SupportTickets.Add(ticket);
            await dbContext.SaveChangesAsync(cancellationToken);

            var response = new Response(
                ticket.TicketId,
                ticket.Status.ToString(),
                ticket.CreatedAt
            );

            return response;
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapPost("/user/support/tickets", async (
                [FromForm] Command command,
                ISender sender = null!,
                CancellationToken cancellationToken = default) =>
            {
                var result = await sender.Send(command, cancellationToken);

                return result.ToHttpResponse();
            })
            .DisableAntiforgery()
            .RequireAuthorization();
        }
    }
}
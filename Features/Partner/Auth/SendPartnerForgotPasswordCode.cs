using Carter;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OfferwallApi.Infrastructure.Interfaces;
using OfferwallApi.Infrastructure.Messaging;
using OfferwallApi.Infrastructure.Persistence;
using OfferwallApi.Infrastructure.Results;
using OfferwallApi.Shared.Entities;

namespace OfferwallApi.Features.Partner.Auth;

public static class SendPartnerForgotPasswordCode
{
    public sealed record Command(string Email) : ICommand<Result>;

    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.Email)
                .NotEmpty()
                .WithMessage("Email is required.")
                .EmailAddress()
                .WithMessage("A valid email address is required.");
        }
    }

    public sealed class Handler(
        ApplicationDbContext dbContext,
        IVerificationCodeService verificationCodeService)
        : ICommandHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            var partner = await dbContext.Partners
                .FirstOrDefaultAsync(p => p.Email == request.Email, cancellationToken);

            if (partner is not null && partner.IsActive)
            {
                await verificationCodeService.SendPartnerVerificationCodeAsync(
                    partner,
                    VerificationCodeType.ForgotPassword,
                    cancellationToken);

                await dbContext.SaveChangesAsync(cancellationToken);
            }

            return Result.Success();
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapPost("/partner/auth/forgot-password/send-code", async (
                Command command,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(command, cancellationToken);

                return result.ToHttpResponse();
            });
        }
    }
}
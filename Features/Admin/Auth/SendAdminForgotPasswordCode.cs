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

public static class SendAdminForgotPasswordCode
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
            var admin = await dbContext.Admins
                .FirstOrDefaultAsync(p => p.Email == request.Email, cancellationToken);

            if (admin is not null)
            {

                var hasActiveCode = await dbContext.AdminVerificationCodes
                .AnyAsync(x =>
                    x.AdminId == admin.AdminId &&
                    x.Type == VerificationCodeType.ForgotPassword &&
                    x.VerifiedAt == null &&
                    x.ExpiresAt > DateTime.UtcNow,
                    cancellationToken);

                if (hasActiveCode)
                    return Result.Failure(AdminErrors.ForgotPasswordCodeAlreadySent);

                await verificationCodeService.SendAdminVerificationCodeAsync(
                    admin,
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
            app.MapPost("/admin/auth/forgot-password/send-code", async (
                Command command,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(command, cancellationToken);

                return result.ToHttpResponse();
            }).AllowAnonymous();
        }
    }
}
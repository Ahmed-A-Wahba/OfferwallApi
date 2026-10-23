using Carter;
using FluentValidation;
using MediatR;
using OfferwallApi.Infrastructure.Interfaces;
using OfferwallApi.Infrastructure.Messaging;
using OfferwallApi.Infrastructure.Persistence;
using OfferwallApi.Infrastructure.Results;
using OfferwallApi.Shared.Entities;
using OfferwallApi.Shared.Errors;
using Microsoft.EntityFrameworkCore;

namespace OfferwallApi.Features.Admin.Auth;

public static class ResendAdminVerificationCode
{
    public sealed record Command(
        string Email
    ) : ICommand<Result>;

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.Email)
                .NotEmpty()
                .EmailAddress()
                .MaximumLength(255);
        }
    }

    internal sealed class Handler(
        ApplicationDbContext dbContext,
        IVerificationCodeService verificationCodeService)
        : ICommandHandler<Command, Result>
    {
        public async Task<Result> Handle(
            Command request,
            CancellationToken cancellationToken)
        {
            var admin = await dbContext.Admins
                .FirstOrDefaultAsync(
                    x => x.Email == request.Email,
                    cancellationToken);

            if (admin is null)
                return Result.Failure(AdminErrors.EmailNotFound);

            var hasActiveCode = await dbContext.AdminVerificationCodes
                .AnyAsync(x =>
                    x.AdminId == admin.AdminId &&
                    x.Type == VerificationCodeType.EmailVerification &&
                    x.VerifiedAt == null &&
                    x.ExpiresAt > DateTime.UtcNow,
                    cancellationToken);

            if (hasActiveCode)
                return Result.Failure(AdminErrors.VerificationCodeAlreadySent);

            await verificationCodeService.SendAdminVerificationCodeAsync(
                admin,
                VerificationCodeType.EmailVerification,
                cancellationToken);

            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapPost(
                "/admin/auth/resend-code",
                async (Command command, ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(command, cancellationToken);
                    return result.ToHttpResponse();
                });
        }
    }
}
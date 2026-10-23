using Carter;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OfferwallApi.Infrastructure.Interfaces;
using OfferwallApi.Infrastructure.Messaging;
using OfferwallApi.Infrastructure.Persistence;
using OfferwallApi.Infrastructure.Results;
using OfferwallApi.Shared.Entities;

namespace OfferwallApi.Features.Admin.Auth;

public static class ResetAdminPasswordWithCode
{
    public sealed record Command(
        string Email,
        string Code,
        string NewPassword
    ) : ICommand<Result>;

    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.Email)
                .NotEmpty()
                .WithMessage("Email is required.")
                .EmailAddress()
                .WithMessage("A valid email address is required.");

            RuleFor(x => x.Code)
                .NotEmpty()
                .WithMessage("Verification code is required.")
                .Length(6)
                .WithMessage("Verification code must be 6 digits.");

            RuleFor(x => x.NewPassword)
                .NotEmpty()
                .MinimumLength(8)
                .MaximumLength(100)
                .Matches(@"[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
                .Matches(@"[a-z]").WithMessage("Password must contain at least one lowercase letter.")
                .Matches(@"\d").WithMessage("Password must contain at least one number.");
        }
    }

    public sealed class Handler(
        ApplicationDbContext dbContext,
        IPasswordHasher passwordHasher)
        : ICommandHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            var admin = await dbContext.Admins
                .FirstOrDefaultAsync(a => a.Email == request.Email, cancellationToken);

            if (admin is null)
            {
                return Error.NotFound("Admin.NotFound", "Invalid request or admin not found.");
            }

            var verificationCode = await dbContext.AdminVerificationCodes
                .Where(c => c.AdminId == admin.AdminId
                         && c.Code == request.Code
                         && c.Type == VerificationCodeType.ForgotPassword)
                .OrderByDescending(c => c.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (verificationCode is null || verificationCode.ExpiresAt < DateTime.UtcNow)
            {
                return Error.Validation("Code.InvalidOrExpired", "Invalid or expired verification code.");
            }

            var password = passwordHasher.Hash(request.NewPassword);
            if (password.IsFailure)
            {
                return password.Errors;
            }

            admin.PasswordHash = password.Value;

            dbContext.AdminVerificationCodes.Remove(verificationCode);

            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapPost("/admin/auth/forgot-password/reset", async (
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
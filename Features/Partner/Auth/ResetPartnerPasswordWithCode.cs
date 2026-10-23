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

public static class ResetPartnerPasswordWithCode
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
            var partner = await dbContext.Partners
                .FirstOrDefaultAsync(p => p.Email == request.Email, cancellationToken);

            if (partner is null || !partner.IsEmailVerified || partner.Status != PublisherStatus.Active)
            {
                return Error.NotFound("Partner.NotFound", "Invalid request or partner not found.");
            }

            var verificationCode = await dbContext.PartnerVerificationCodes
                .Where(c => c.PartnerId == partner.PartnerId
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

            partner.PasswordHash = password.Value;

            dbContext.PartnerVerificationCodes.Remove(verificationCode);

            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapPost("/partner/auth/forgot-password/reset", async (
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
using Carter;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OfferwallApi.Infrastructure.Messaging;
using OfferwallApi.Infrastructure.Persistence;
using OfferwallApi.Infrastructure.Results;
using OfferwallApi.Shared.Entities;
using OfferwallApi.Shared.Errors;

namespace OfferwallApi.Features.Partner.Auth;

public static class VerifyPartnerEmail
{
    public sealed record Command(
        string Email,
        string Code
    ) : ICommand<Result>;

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.Email)
                .NotEmpty()
                .EmailAddress()
                .MaximumLength(255);

            RuleFor(x => x.Code)
                .NotEmpty()
                .Length(6);
        }
    }

    internal sealed class Handler(
        ApplicationDbContext dbContext
    ) : ICommandHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            var partner = await dbContext.Partners
                .FirstOrDefaultAsync(
                    x => x.Email == request.Email,
                    cancellationToken);

            if (partner is null)
                return Result.Failure(PartnerErrors.EmailNotFound);

            if (partner.IsEmailVerified)
                return Result.Failure(PartnerErrors.EmailAlreadyVerified);

            var verificationCode = await dbContext.PartnerVerificationCodes
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefaultAsync(
                    x =>
                        x.PartnerId == partner.PartnerId &&
                        x.Type == VerificationCodeType.EmailVerification &&
                        x.VerifiedAt == null,
                    cancellationToken);

            if (verificationCode is null)
                return Result.Failure(PartnerErrors.VerificationCodeNotFound);

            if (verificationCode.ExpiresAt <= DateTime.UtcNow)
            {
                dbContext.PartnerVerificationCodes.Remove(verificationCode);

                await dbContext.SaveChangesAsync(cancellationToken);

                return Result.Failure(PartnerErrors.VerificationCodeExpired);
            }


            if (verificationCode.Code != request.Code)
            {
                verificationCode.AttemptCount++;

                if (verificationCode.AttemptCount >= 5)
                {
                    dbContext.PartnerVerificationCodes.Remove(verificationCode);

                    await dbContext.SaveChangesAsync(cancellationToken);

                    return Result.Failure(PartnerErrors.VerificationCodeExpired);
                }

                await dbContext.SaveChangesAsync(cancellationToken);

                return Result.Failure(PartnerErrors.InvalidVerificationCode);
            }

            verificationCode.VerifiedAt = DateTime.UtcNow;

            partner.IsEmailVerified = true;
            partner.IsActive = false;
            partner.UpdatedAt = DateTime.UtcNow;

            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapPost(
                "/partner/auth/verify-email",
                async (Command command, ISender sender) =>
                {
                    var result = await sender.Send(command);
                    return result.ToHttpResponse();
                });
        }
    }
}
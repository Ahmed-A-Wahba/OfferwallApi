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

namespace OfferwallApi.Features.PartnerAuth;

public static class ResendVerificationCode
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
            var partner = await dbContext.Partners
                .FirstOrDefaultAsync(
                    x => x.Email == request.Email,
                    cancellationToken);

            if (partner is null)
                return Result.Failure(PartnerErrors.EmailNotFound);

            if (partner.IsEmailVerified)
                return Result.Failure(PartnerErrors.EmailAlreadyVerified);

            var hasActiveCode = await dbContext.PartnerVerificationCodes
                .AnyAsync(x =>
                    x.PartnerId == partner.PartnerId &&
                    x.Type == VerificationCodeType.EmailVerification &&
                    x.VerifiedAt == null &&
                    x.ExpiresAt > DateTime.UtcNow,
                    cancellationToken);

            if (hasActiveCode)
                return Result.Failure(PartnerErrors.VerificationCodeAlreadySent);

            await verificationCodeService.SendPartnerVerificationCodeAsync(
                partner,
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
                "/partner/auth/resend-code",
                async (Command command, ISender sender) =>
                {
                    var result = await sender.Send(command);
                    return result.ToHttpResponse();
                });
        }
    }
}
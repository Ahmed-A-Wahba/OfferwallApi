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

public static class AdminVerifyOtp
{
    public sealed record Command(
        string Email,
        string Code
    ) : ICommand<Result<Response>>;

    public sealed record Response(
        string AccessToken,
        string RefreshToken,
        int ExpiresIn
    );

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
        ApplicationDbContext dbContext,
        IJwtService jwtService,
        IJwtClaimsFactory claimsFactory
    ) : ICommandHandler<Command, Result<Response>>
    {
        public async Task<Result<Response>> Handle(Command request, CancellationToken cancellationToken)
        {
            var admin = await dbContext.Admins
                .FirstOrDefaultAsync(
                    x => x.Email == request.Email,
                    cancellationToken);

            if (admin is null)
                return AdminErrors.InvalidCredentials;

            var verificationCode = await dbContext.AdminVerificationCodes
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefaultAsync(
                    x =>
                        x.AdminId == admin.AdminId &&
                        x.Type == VerificationCodeType.EmailVerification &&
                        x.VerifiedAt == null,
                    cancellationToken);

            if (verificationCode is null)
                return AdminErrors.InvalidCode;

            System.Console.WriteLine($"Verification Code: {verificationCode.Code}, Expires At: {verificationCode.ExpiresAt}, Attempt Count: {verificationCode.AttemptCount}");

            if (verificationCode.ExpiresAt <= DateTime.UtcNow)
            {
                dbContext.AdminVerificationCodes.Remove(verificationCode);

                await dbContext.SaveChangesAsync(cancellationToken);

                return AdminErrors.ExpiredCode;
            }

            if (verificationCode.Code.Trim() != request.Code.Trim())
            {
                verificationCode.AttemptCount++;

                if (verificationCode.AttemptCount >= 5)
                {
                    dbContext.AdminVerificationCodes.Remove(verificationCode);

                    await dbContext.SaveChangesAsync(cancellationToken);

                    return AdminErrors.ExpiredCode;
                }

                await dbContext.SaveChangesAsync(cancellationToken);

                return AdminErrors.InvalidCode;
            }

            // 4. نجاح التحقق: توليد التوكنز
            var accessToken = jwtService.GenerateAccessToken(claimsFactory.Create(admin));

            var refreshToken = jwtService.GenerateRefreshToken();

            dbContext.AdminRefreshTokens.Add(
                new AdminRefreshToken
                {
                    RefreshTokenId = Guid.CreateVersion7(),
                    AdminId = admin.AdminId,
                    Token = refreshToken,
                    ExpiresAt = DateTime.UtcNow.AddDays(30),
                    CreatedAt = DateTime.UtcNow
                });

            dbContext.AdminVerificationCodes.Remove(verificationCode);

            await dbContext.SaveChangesAsync(cancellationToken);

            return new Response(
                accessToken,
                refreshToken,
                jwtService.ExpiresIn);
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapPost(
                "/admin/auth/verify-otp",
                async (Command command, ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(command, cancellationToken);
                    return result.ToHttpResponse();
                });
        }
    }
}
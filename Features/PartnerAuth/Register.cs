using FluentValidation;
using OfferwallApi.Infrastructure.Interfaces;
using OfferwallApi.Infrastructure.Messaging;
using OfferwallApi.Infrastructure.Persistence;
using OfferwallApi.Infrastructure.Results;
using Microsoft.EntityFrameworkCore;
using OfferwallApi.Shared.Entities;
using OfferwallApi.Shared.Errors;
using MediatR;
using Carter;

namespace OfferwallApi.Features.PartnerAuth;

public static class Register
{
    public sealed record Command(
        string FullName,
        string Email,
        string CompanyName,
        string Password,
        string WebsiteUrl,
        Country CountryCode,
        int MonthlyActiveUsers,
        StoreCategory StoreCategory,
        string? AllowedIframeOrigin
    ) : ICommand<Result>;

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.FullName)
                .NotEmpty()
                .MinimumLength(3)
                .MaximumLength(150);

            RuleFor(x => x.Email)
                .NotEmpty()
                .EmailAddress()
                .MaximumLength(255);

            RuleFor(x => x.CompanyName)
                .NotEmpty()
                .MinimumLength(2)
                .MaximumLength(200);

            RuleFor(x => x.Password)
                .NotEmpty()
                .MinimumLength(8)
                .MaximumLength(100)
                .Matches(@"[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
                .Matches(@"[a-z]").WithMessage("Password must contain at least one lowercase letter.")
                .Matches(@"\d").WithMessage("Password must contain at least one number.");

            RuleFor(x => x.WebsiteUrl)
                .NotEmpty()
                .MaximumLength(500)
                .Must(uri => Uri.TryCreate(uri, UriKind.Absolute, out _))
                .WithMessage("Website URL is invalid.");

            RuleFor(x => x.CountryCode)
                .IsInEnum();

            RuleFor(x => x.MonthlyActiveUsers)
                .GreaterThan(0);

            RuleFor(x => x.StoreCategory)
                .IsInEnum();

            When(x => !string.IsNullOrWhiteSpace(x.AllowedIframeOrigin), () =>
            {
                RuleFor(x => x.AllowedIframeOrigin!)
                    .NotEmpty()
                    .MaximumLength(255)
                    .Must(origin =>
                    {
                        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                            return false;

                        return uri.Scheme == Uri.UriSchemeHttps
                               || uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase);
                    })
                    .WithMessage("Allowed iframe origin must be a valid HTTPS URL or localhost.");
            });
        }
    }

    internal sealed class Handler(
    ApplicationDbContext dbContext,
    IPasswordHasher passwordHasher,
    IApiKeyGenerator apiKeyGenerator,
    IVerificationCodeService verificationCodeService
) : ICommandHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            var emailExists = await dbContext.Partners
                .AnyAsync(x => x.Email == request.Email, cancellationToken);

            if (emailExists)
            {
                return Result.Failure(PartnerErrors.EmailAlreadyExists);
            }
            var hashResult = passwordHasher.Hash(request.Password);
            if (hashResult.IsFailure)
            {
                return Result.Failure(hashResult.Error);
            }

            var partner = new Partner
            {
                PartnerId = Guid.CreateVersion7(),

                FullName = request.FullName,
                Email = request.Email,
                CompanyName = request.CompanyName,

                PasswordHash = hashResult.Value,

                WebsiteUrl = request.WebsiteUrl,

                CountryCode = request.CountryCode,
                StoreCategory = request.StoreCategory,
                MonthlyActiveUsers = request.MonthlyActiveUsers,

                ApiKey = apiKeyGenerator.GenerateApiKey(),

                AllowedIframeOrigin = request.AllowedIframeOrigin,

                PointsPerUsd = 100,

                IsActive = false,
                IsEmailVerified = false,

                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            dbContext.Partners.Add(partner);

            await verificationCodeService.SendPartnerVerificationCodeAsync(partner, VerificationCodeType.EmailVerification, cancellationToken);

            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapPost("/partners/register", async (Command command, ISender sender) =>
                {
                    var result = await sender.Send(command);
                    return result.ToHttpResponse();
                });
        }
    }
}
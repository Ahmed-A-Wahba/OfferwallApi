using Carter;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfferwallApi.Infrastructure.Authorization;
using OfferwallApi.Infrastructure.Interfaces;
using OfferwallApi.Infrastructure.Messaging;
using OfferwallApi.Infrastructure.Persistence;
using OfferwallApi.Infrastructure.Results;

namespace OfferwallApi.Features.Partner.Settings;

public static class UpdatePartnerProfile
{
    [Authorize(Role = "Partner")]
    public sealed record Command(
        string FullName,
        string? PhoneNumber,
        IFormFile? Logo
    ) : ICommand<Result>;

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.FullName)
                .NotEmpty()
                .MaximumLength(200);

            RuleFor(x => x.PhoneNumber)
                .MaximumLength(30);

            When(x => x.Logo is not null, () =>
            {
                RuleFor(x => x.Logo!)
                    .Must(file => file.Length <= 5 * 1024 * 1024)
                    .WithMessage("Logo size must not exceed 5MB.")
                    .Must(file => new[] { ".jpg", ".jpeg", ".png", ".webp" }
                        .Contains(Path.GetExtension(file.FileName).ToLower()))
                    .WithMessage("Invalid file type. Allowed formats: JPG, JPEG, PNG, WEBP.")
                    .Must(file => new[] { "image/jpeg", "image/jpg", "image/png", "image/webp" }
                        .Contains(file.ContentType.ToLower()))
                    .WithMessage("Invalid content type. File must be a valid image (JPEG, PNG, or WEBP).");
            });
        }
    }

    internal sealed class Handler(
    ApplicationDbContext context,
    ICurrentUser currentUser,
    IStorageService storageService)
    : ICommandHandler<Command, Result>
    {
        public async Task<Result> Handle(
            Command request,
            CancellationToken cancellationToken)
        {
            var partnerId = currentUser.UserId;

            var partner = await context.Partners
                .FirstOrDefaultAsync(
                    x => x.PartnerId == partnerId,
                    cancellationToken);

            if (partner is null)
            {
                return Error.NotFound(
                    "PartnerNotFound",
                    "The partner was not found.");
            }

            if (request.Logo is not null && request.Logo.Length > 0)
            {
                if (!string.IsNullOrEmpty(partner.LogoUrl))
                {
                    await storageService.DeleteFileAsync(partner.LogoUrl, cancellationToken);
                }

                partner.LogoUrl = await storageService.SaveFileAsync(request.Logo, "Partner", cancellationToken);
            }

            partner.FullName = request.FullName;
            partner.PhoneNumber = request.PhoneNumber;

            await context.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapPut(
                "/partner/settings/profile",
                async (
                    [FromForm] string fullName,
                    [FromForm] string? phoneNumber,
                    IFormFile? logo,
                    ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    var command = new Command(fullName, phoneNumber, logo);

                    var result = await sender.Send(
                        command,
                        cancellationToken);

                    return result.ToHttpResponse();
                })
                .DisableAntiforgery();
        }
    }
}

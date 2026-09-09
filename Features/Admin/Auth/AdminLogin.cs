using System.Security.Cryptography;
using Carter;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using OfferwallApi.Infrastructure.Interfaces;
using OfferwallApi.Infrastructure.Persistence;
using OfferwallApi.Infrastructure.Results;
using OfferwallApi.Shared.Entities;

namespace OfferwallApi.Features.Admin.Auth;

public static class AdminLogin
{
    public sealed record Command(string Email, string Password) : IRequest<Result>;

    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("Email is required.")
                .EmailAddress().WithMessage("Invalid email format.");

            RuleFor(x => x.Password)
                .NotEmpty().WithMessage("Password is required.");
        }
    }

    public sealed class Handler(
        ApplicationDbContext dbContext,
        IPasswordHasher passwordHasher,
        IVerificationCodeService verificationCodeService) : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {

            var admin = await dbContext.Admins
                .FirstOrDefaultAsync(a => a.Email == request.Email, cancellationToken);

            if (admin is null || !passwordHasher.Verify(request.Password, admin.PasswordHash))
            {
                return Error.Failure("InvalidCredentials", "Invalid email or password.");
            }

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
        public sealed record LoginRequest(string Email, string Password);

        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapPost("/admin/auth/login", async (
                [FromBody] LoginRequest body,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var command = new Command(body.Email, body.Password);
                var result = await sender.Send(command, cancellationToken);

                return result.ToHttpResponse();
            }).AllowAnonymous();
        }
    }
}
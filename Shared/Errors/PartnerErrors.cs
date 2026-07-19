using OfferwallApi.Infrastructure.Results;

namespace OfferwallApi.Shared.Errors;

public static class PartnerErrors
{
    public static readonly Error EmailAlreadyExists =
        Error.Conflict(
            "Partner.EmailAlreadyExists",
            "A partner with this email already exists.");

    public static readonly Error InvalidVerificationCode =
        Error.Validation(
            "Partner.InvalidVerificationCode",
            "The verification code is invalid.");

    public static readonly Error VerificationCodeExpired =
        Error.Validation(
            "Partner.VerificationCodeExpired",
            "The verification code has expired.");

    public static readonly Error EmailNotVerified =
        Error.Unauthorized(
            "Partner.EmailNotVerified",
            "Please verify your email first.");

    public static readonly Error VerificationCodeAlreadySent =
        Error.Validation(
            "Partner.VerificationCodeAlreadySent",
            "A verification code has already been sent. Please check your email.");

    public static readonly Error EmailNotFound =
        Error.Unauthorized(
            "Partner.EmailNotFound",
            "No partner found with this email.");

    public static readonly Error EmailAlreadyVerified =
        Error.Validation(
            "Partner.EmailAlreadyVerified",
            "This email has already been verified.");
}
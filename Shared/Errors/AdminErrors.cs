using OfferwallApi.Infrastructure.Results;

namespace OfferwallApi.Shared.Errors;

public static class AdminErrors
{
    public static readonly Error InvalidCredentials = Error.Unauthorized("InvalidCredentials", "Invalid email or password.");
    public static readonly Error InvalidCode = Error.Validation("InvalidCode", "Invalid verification code.");
    public static readonly Error ExpiredCode = Error.Validation("ExpiredCode", "Verification code has expired.");
    public static readonly Error EmailNotFound = Error.NotFound("EmailNotFound", "Admin with the provided email does not exist.");
    public static readonly Error EmailAlreadyVerified = Error.Validation("EmailAlreadyVerified", "Admin email is already verified.");
    public static readonly Error VerificationCodeAlreadySent = Error.Validation("VerificationCodeAlreadySent", "A verification code has already been sent to this email.");
    public static readonly Error InvalidRefreshToken = Error.Unauthorized("InvalidRefreshToken", "The provided refresh token is invalid.");
    public static readonly Error EmailNotVerified = Error.Unauthorized("EmailNotVerified", "Admin email is not verified.");
    public static readonly Error AccountPendingApproval = Error.Unauthorized("AccountPendingApproval", "Admin account is pending approval.");
    public static readonly Error PasswordResetNotAllowed = Error.Validation("PasswordResetNotAllowed", "Password reset is not allowed for this admin.");
    public static readonly Error ForgotPasswordCodeAlreadySent = Error.Validation("ForgotPasswordCodeAlreadySent", "A forgot password code has already been sent to this email.");
}
using OfferwallApi.Infrastructure.Results;

namespace OfferwallApi.Shared.Errors;

public static class AdminErrors
{
    public static readonly Error InvalidCredentials = Error.Unauthorized("InvalidCredentials", "Invalid email or password.");
    public static readonly Error InvalidCode = Error.Validation("InvalidCode", "Invalid verification code.");
    public static readonly Error ExpiredCode = Error.Validation("ExpiredCode", "Verification code has expired.");
}
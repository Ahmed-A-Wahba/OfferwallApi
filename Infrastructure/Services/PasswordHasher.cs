using System.Text.RegularExpressions;
using OfferwallApi.Infrastructure.Interfaces;
using OfferwallApi.Infrastructure.Results;

namespace OfferwallApi.Infrastructure.Services;

public partial class PasswordHasher : IPasswordHasher
{
    private static readonly Regex PasswordRegex = StrongPasswordRegex();
    public Result<string> Hash(string password)
    {
        return PasswordRegex.IsMatch(password)
            ? BCrypt.Net.BCrypt.EnhancedHashPassword(password)
            : Error.Validation("Password too weak", "Password must be at least 8 characters long and contain at least one uppercase letter, one lowercase letter, one number, and one special character.");
    }

    public bool Verify(string password, string passwordHash)
    {
        return BCrypt.Net.BCrypt.EnhancedVerify(password, passwordHash);
    }
    public string HashCode(string code)
    {
        return BCrypt.Net.BCrypt.EnhancedHashPassword(code);
    }

    public bool VerifyCode(string code, string codeHash)
    {
        return BCrypt.Net.BCrypt.EnhancedVerify(code, codeHash);
    }

    [GeneratedRegex("^(?=.*?[A-Z])(?=.*?[a-z])(?=.*?[0-9])(?=.*?[#?!@$%^&*-]).{8,}$", RegexOptions.Compiled)]
    private static partial Regex StrongPasswordRegex();

}
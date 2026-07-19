using OfferwallApi.Infrastructure.Results;

namespace OfferwallApi.Infrastructure.Interfaces;

public interface IPasswordHasher
{
    Result<string> Hash(string password);
    bool VerifyPassword(string password, string passwordHash);
}
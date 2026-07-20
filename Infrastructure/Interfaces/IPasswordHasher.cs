using OfferwallApi.Infrastructure.Results;

namespace OfferwallApi.Infrastructure.Interfaces;

public interface IPasswordHasher
{
    Result<string> Hash(string password);
    bool Verify(string password, string passwordHash);
}
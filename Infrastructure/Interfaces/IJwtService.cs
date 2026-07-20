using System.Security.Claims;

namespace OfferwallApi.Infrastructure.Interfaces;

public interface IJwtService
{
    int ExpiresIn { get; }
    string GenerateAccessToken(IEnumerable<Claim> claims);

    string GenerateRefreshToken();
}
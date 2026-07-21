using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using OfferwallApi.Infrastructure.Interfaces;

namespace OfferwallApi.Infrastructure.Services;

public sealed class CurrentUser(
    IHttpContextAccessor httpContextAccessor)
    : ICurrentUser
{
    private readonly ClaimsPrincipal? _user = httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated =>
        _user?.Identity?.IsAuthenticated ?? false;

    public Guid UserId =>
        Guid.Parse(
            _user!.FindFirstValue(JwtRegisteredClaimNames.Sub)!);

    public string Email =>
        _user!.FindFirstValue(JwtRegisteredClaimNames.Email)!;

    public string Role =>
        _user!.FindFirstValue(ClaimTypes.Role)!;
}
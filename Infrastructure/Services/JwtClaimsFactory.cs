using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using OfferwallApi.Infrastructure.Interfaces;
using OfferwallApi.Shared.Entities;

namespace OfferwallApi.Infrastructure.Services;

public sealed class JwtClaimsFactory : IJwtClaimsFactory
{
    public Claim[] Create(Partner partner)
    {
        return
        [
            new(
                JwtRegisteredClaimNames.Sub,
                partner.PartnerId.ToString()),

            new(
                JwtRegisteredClaimNames.Email,
                partner.Email),

            new(
                JwtRegisteredClaimNames.Jti,
                Guid.CreateVersion7().ToString()),

            new(
                ClaimTypes.Role,
                "Partner")
        ];
    }

    public Claim[] Create(Admin admin)
    {
        return
        [
            new(
                JwtRegisteredClaimNames.Sub,
                admin.AdminId.ToString()),

            new(
                JwtRegisteredClaimNames.Email,
                admin.Email),

            new(
                JwtRegisteredClaimNames.Jti,
                Guid.CreateVersion7().ToString()),

            new(
                ClaimTypes.Role,
                "Admin")
        ];
    }

    public Claim[] Create(User user)
    {
        return
        [
            new(
            JwtRegisteredClaimNames.Sub,
            user.UserId.ToString()),

        new(
            "partner_id",
            user.PartnerId.ToString()),

        new(
            "external_user_id",
            user.ExternalUserId),

        new(
            JwtRegisteredClaimNames.Jti,
            Guid.CreateVersion7().ToString()),

        new(
            ClaimTypes.Role,
            "User")
        ];
    }
}
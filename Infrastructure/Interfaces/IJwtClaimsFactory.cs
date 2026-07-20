using System.Security.Claims;
using OfferwallApi.Shared.Entities;

namespace OfferwallApi.Infrastructure.Interfaces;

public interface IJwtClaimsFactory
{
    Claim[] Create(Partner partner);

    Claim[] Create(Admin admin);
}
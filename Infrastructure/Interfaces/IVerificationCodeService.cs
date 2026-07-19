using OfferwallApi.Shared.Entities;

namespace OfferwallApi.Infrastructure.Interfaces;

public interface IVerificationCodeService
{
    Task SendPartnerVerificationCodeAsync(
        Partner partner,
        VerificationCodeType type,
        CancellationToken cancellationToken = default);
}
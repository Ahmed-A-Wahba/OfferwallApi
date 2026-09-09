namespace OfferwallApi.Infrastructure.Interfaces;

public interface IUserLocationService
{
    Task<string?> GetCountryCodeAsync(CancellationToken cancellationToken = default);
}
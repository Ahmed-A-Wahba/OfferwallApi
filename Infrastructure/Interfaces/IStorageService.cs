namespace OfferwallApi.Infrastructure.Interfaces;

public interface IStorageService
{
    Task<string> SaveFileAsync(IFormFile file, string folder, CancellationToken cancellationToken);

    Task DeleteFileAsync(string Url, CancellationToken cancellationToken);
}
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Options;
using OfferwallApi.Infrastructure.Interfaces;
using OfferwallApi.Infrastructure.Options;

namespace OfferwallApi.Infrastructure.Services;

public class CloudinaryStorageService : IStorageService
{
    private readonly Cloudinary _cloudinary;

    public CloudinaryStorageService(IOptions<CloudinarySettings> config)
    {
        var acc = new Account(
            config.Value.CloudName,
            config.Value.ApiKey,
            config.Value.ApiSecret
        );

        _cloudinary = new Cloudinary(acc);
    }

    public async Task DeleteFileAsync(string url, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(url)) return;

        var publicId = ExtractPublicIdFromUrl(url);

        if (string.IsNullOrEmpty(publicId)) return;

        try
        {
            var deleteParams = new DeletionParams(publicId)
            {
                ResourceType = ResourceType.Image
            };

            await _cloudinary.DestroyAsync(deleteParams);
        }
        catch (Exception)
        {
            // Deletion failures are non-critical — log and continue.
            // The old file will remain in Cloudinary storage but won't affect the partner's profile.
        }
    }

    public async Task<string> SaveFileAsync(IFormFile file, string folder, CancellationToken cancellationToken)
    {
        if (file.Length <= 0) return string.Empty;

        using var stream = file.OpenReadStream();

        var isImage = file.ContentType.StartsWith("image/");

        var uploadParams = new RawUploadParams
        {
            File = new FileDescription(file.FileName, stream),
            Folder = $"Nexus/{folder}",
            PublicId = Guid.NewGuid().ToString()
        };

        if (isImage)
        {
            var imageParams = new ImageUploadParams
            {
                File = new FileDescription(file.FileName, stream),
                Folder = $"Nexus/{folder}",
                Transformation = new Transformation().Quality("auto").FetchFormat("auto")
            };

            var imageResult = await _cloudinary.UploadAsync(imageParams, cancellationToken);
            return imageResult.SecureUrl.ToString();
        }

        var result = await _cloudinary.UploadAsync(uploadParams, "raw");
        return result.SecureUrl.ToString();
    }

    private static string ExtractPublicIdFromUrl(string url)
    {
        try
        {
            var uri = new Uri(url);
            var pathSegments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

            var uploadIndex = Array.IndexOf(pathSegments, "upload");
            if (uploadIndex == -1 || uploadIndex >= pathSegments.Length - 1)
                return string.Empty;

            var startIndex = uploadIndex + 1;
            if (pathSegments[startIndex].StartsWith('v') && long.TryParse(pathSegments[startIndex][1..], out _))
            {
                startIndex++;
            }

            var relativePathSegments = pathSegments[startIndex..];
            var fullPath = string.Join('/', relativePathSegments);

            var lastDotIndex = fullPath.LastIndexOf('.');
            return lastDotIndex > 0 ? fullPath[..lastDotIndex] : fullPath;
        }
        catch
        {
            return string.Empty;
        }
    }
}
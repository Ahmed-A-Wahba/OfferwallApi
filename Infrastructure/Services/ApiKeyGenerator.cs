using System.Security.Cryptography;
using OfferwallApi.Infrastructure.Interfaces;

namespace OfferwallApi.Infrastructure.Services;

public sealed class ApiKeyGenerator : IApiKeyGenerator
{
    private const string Prefix = "nexus_live_";

    public string GenerateApiKey()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);

        var key = Convert.ToHexString(bytes).ToLowerInvariant();

        return $"{Prefix}{key}";
    }
}
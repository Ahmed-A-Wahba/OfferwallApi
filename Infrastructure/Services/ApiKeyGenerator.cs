using System.Security.Cryptography;
using OfferwallApi.Infrastructure.Interfaces;

namespace OfferwallApi.Infrastructure.Services;

public sealed class ApiKeyGenerator : IApiKeyGenerator
{
    private const string ApiKeyPrefix = "nexus_live_";
    private const string ApiSecretPrefix = "nexus_sec_";

    public string GenerateApiKey()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);

        var key = Convert.ToHexString(bytes).ToLowerInvariant();

        return $"{ApiKeyPrefix}{key}";
    }

    public string GenerateApiSecret()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);

        var secret = Convert.ToHexString(bytes).ToLowerInvariant();

        return $"{ApiSecretPrefix}{secret}";
    }
}
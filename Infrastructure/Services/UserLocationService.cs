using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using OfferwallApi.Infrastructure.Interfaces;

namespace OfferwallApi.Infrastructure.Services;

public sealed class UserLocationService(
    HttpClient httpClient,
    IHttpContextAccessor httpContextAccessor)
    : IUserLocationService
{
    public async Task<string?> GetCountryCodeAsync(CancellationToken cancellationToken = default)
    {
        var context = httpContextAccessor.HttpContext;
        if (context is null) return null;

        var ipAddress = GetClientIpAddress(context);

        if (string.IsNullOrWhiteSpace(ipAddress) || isLocalIp(ipAddress))
        {
            return "DE";
        }

        try
        {
            var response = await httpClient.GetFromJsonAsync<IpApiResponse>(
                $"http://ip-api.com/json/{ipAddress}?fields=status,countryCode",
                cancellationToken);

            return response?.Status == "success" ? response.CountryCode : null;
        }
        catch
        {
            return null;
        }
    }

    private static string? GetClientIpAddress(HttpContext context)
    {
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwardedFor))
        {
            return forwardedFor.Split(',')[0].Trim();
        }

        return context.Connection.RemoteIpAddress?.ToString();
    }

    private static bool isLocalIp(string ip) =>
        ip is "127.0.0.1" or "::1" or "localhost" || ip.StartsWith("192.168.") || ip.StartsWith("10.");

    private sealed record IpApiResponse(
        [property: JsonPropertyName("status")] string Status,
        [property: JsonPropertyName("countryCode")] string CountryCode
    );
}
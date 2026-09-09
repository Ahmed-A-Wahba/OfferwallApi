using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.WebUtilities;
using OfferwallApi.Infrastructure.Interfaces;
using OfferwallApi.Infrastructure.Options;
using static OfferwallApi.Infrastructure.Interfaces.IKlinkApiClient;

namespace OfferwallApi.Infrastructure.Services;

public sealed class KlinkApiClient : IKlinkApiClient
{
    private readonly HttpClient _httpClient;

    public KlinkApiClient(HttpClient httpClient, IOptions<KlinkOptions> options)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(options.Value.BaseUrl);

        var bearerToken = $"{options.Value.PubId}:{options.Value.ApiKey}";

        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", bearerToken);
    }

    public async Task<KlinkOffersResponse?> GetOffersAsync(
        string? country,
        string? platform,
        string? deviceName,
        string? category,
        string? sortBy,
        int page,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var queryParams = new Dictionary<string, string?>();

        if (!string.IsNullOrWhiteSpace(country)) queryParams["country"] = country;
        if (!string.IsNullOrWhiteSpace(platform)) queryParams["platform"] = platform;
        if (!string.IsNullOrWhiteSpace(deviceName)) queryParams["device_name"] = deviceName;
        if (!string.IsNullOrWhiteSpace(category)) queryParams["category"] = category;
        if (!string.IsNullOrWhiteSpace(sortBy)) queryParams["sort_by"] = sortBy;

        queryParams["page"] = page.ToString();
        queryParams["limit"] = limit.ToString();

        var requestUri = QueryHelpers.AddQueryString("offers", queryParams);

        var response = await _httpClient.GetAsync(requestUri, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        return JsonSerializer.Deserialize<KlinkOffersResponse>(content, options);
    }
}
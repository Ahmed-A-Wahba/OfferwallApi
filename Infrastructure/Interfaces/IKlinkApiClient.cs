namespace OfferwallApi.Infrastructure.Interfaces;

public interface IKlinkApiClient
{
    Task<KlinkOffersResponse?> GetOffersAsync(
        string? country,
        string? platform,
        string? deviceName,
        string? category,
        string? sortBy,
        int page,
        int limit,
        CancellationToken cancellationToken = default);
    public sealed record KlinkOffersResponse(
        List<KlinkOfferDto> Data,
        int Total,
        int CurrentPage,
        int TotalPages,
        bool Success
    );

    public sealed record KlinkOfferDto(
        string OfferId,
        Dictionary<string, string>? Name,
        Dictionary<string, string>? Description,
        string Platform,
        string DeviceName,
        List<KlinkActivityDto>? Activities,
        decimal TotalPayout,
        decimal Epc,
        KlinkImageDto? Images,
        List<string>? Countries,
        List<string>? Categories,
        bool IsActive
    );

    public sealed record KlinkActivityDto(
        string Name,
        string EventId,
        string EventName,
        int Order,
        decimal Payout
    );

    public sealed record KlinkImageDto(
        string Logo
    );
}
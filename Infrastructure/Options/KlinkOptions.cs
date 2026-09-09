namespace OfferwallApi.Infrastructure.Options;

public sealed class KlinkOptions
{
    public const string SectionName = "KlinkOptions";

    public string BaseUrl { get; init; } = string.Empty;
    public string ApiKey { get; init; } = string.Empty;
    public string PubId { get; init; } = string.Empty;
    public string ApiSecret { get; init; } = string.Empty;
}
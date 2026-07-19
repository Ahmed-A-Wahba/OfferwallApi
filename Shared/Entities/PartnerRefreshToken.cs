using OfferwallApi.Shared.Entities;

namespace OfferwallApi.Entities;

public sealed class PartnerRefreshToken
{
    public Guid RefreshTokenId { get; set; }

    public Guid PartnerId { get; set; }

    public string Token { get; set; } = null!;

    public DateTime ExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    public Partner Partner { get; set; } = null!;
}
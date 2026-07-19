using OfferwallApi.Shared.Entities;

namespace OfferwallApi.Entities;

public sealed class AdminRefreshToken
{
    public Guid RefreshTokenId { get; set; }

    public Guid AdminId { get; set; }

    public string Token { get; set; } = null!;

    public DateTime ExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    public Admin Admin { get; set; } = null!;
}
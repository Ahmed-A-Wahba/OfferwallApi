using OfferwallApi.Entities;

namespace OfferwallApi.Shared.Entities;

public sealed class Admin
{
    public Guid AdminId { get; set; }

    public string Email { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public ICollection<AdminRefreshToken> RefreshTokens { get; set; } = [];

    public ICollection<AdminVerificationCode> VerificationCodes { get; set; } = [];
}
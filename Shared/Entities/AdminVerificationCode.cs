namespace OfferwallApi.Shared.Entities;

public sealed class AdminVerificationCode
{
    public static readonly int MaxAllowedAttempts = 3;

    public Guid VerificationCodeId { get; set; }

    public Guid AdminId { get; set; }

    public string Code { get; set; } = null!;

    public VerificationCodeType Type { get; set; }

    public DateTime ExpiresAt { get; set; }

    public DateTime? VerifiedAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public int AttemptCount { get; set; } = 0;

    public Admin Admin { get; set; } = null!;
}

public enum VerificationCodeType
{
    EmailVerification = 1,
    ForgotPassword = 2
}
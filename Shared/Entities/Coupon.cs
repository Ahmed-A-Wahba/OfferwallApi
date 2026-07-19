using OfferwallApi.Shared.Entities;

namespace OfferwallApi.Entities;

public sealed class Coupon
{
    public Guid CouponId { get; set; }

    public Guid UserId { get; set; }

    public Guid PartnerId { get; set; }

    public string Code { get; set; } = null!;

    public decimal Value { get; set; }

    public string CountryCode { get; set; } = null!;

    public string CityCode { get; set; } = null!;

    public CouponStatus Status { get; set; }

    public DateTime ExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public DateTime? RedeemedAt { get; set; }

    // Navigation Properties

    public User User { get; set; } = null!;

    public Partner Partner { get; set; } = null!;

    public ICollection<WalletTransaction> WalletTransactions { get; set; } = [];
}
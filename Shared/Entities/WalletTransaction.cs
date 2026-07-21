namespace OfferwallApi.Shared.Entities;

public sealed class WalletTransaction
{
    public Guid TransactionId { get; set; }

    public Guid UserId { get; set; }

    public decimal Amount { get; set; }

    public WalletTransactionType Type { get; set; }

    public Guid? ConversionId { get; set; }

    public Guid? CouponId { get; set; }

    public DateTime CreatedAt { get; set; }

    public User User { get; set; } = null!;

    public Conversion? Conversion { get; set; }

    public Coupon? Coupon { get; set; }
}

public enum WalletTransactionType
{
    OfferReward = 1,
    CouponRedeem = 2,
    Chargeback = 3,
    ManualAdjustment = 4,
    Bonus = 5
}
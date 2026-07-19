namespace OfferwallApi.Entities;

public sealed class Conversion
{
    public Guid ConversionId { get; set; }

    public Guid UserId { get; set; }

    public string OfferId { get; set; } = null!;

    public string EventId { get; set; } = null!;

    public decimal Payout { get; set; }

    public ConversionType Type { get; set; }

    public Guid? ReversedConversionId { get; set; }

    public DateTime CreatedAt { get; set; }

    public User User { get; set; } = null!;

    public ICollection<WalletTransaction> WalletTransactions { get; set; } = [];
}

public enum ConversionType
{
    Conversion = 1,
    Chargeback = 2
}

public enum CouponStatus
{
    Active = 1,
    Redeemed = 2,
    Expired = 3
}
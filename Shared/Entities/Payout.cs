namespace OfferwallApi.Shared.Entities;

public sealed class Payout
{
    public Guid PayoutId { get; set; }

    public Guid PartnerId { get; set; }

    public decimal Amount { get; set; }

    public int RedemptionCount { get; set; }

    public DateTime ExpectedPayoutDate { get; set; }

    public DateTime PeriodStart { get; set; }

    public DateTime PeriodEnd { get; set; }

    public PayoutStatus Status { get; set; }

    public DateTime? PaidAt { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }

    public Partner Partner { get; set; } = null!;
}

public enum PayoutStatus
{
    Pending = 1,
    Paid = 2,
    Cancelled = 3
}
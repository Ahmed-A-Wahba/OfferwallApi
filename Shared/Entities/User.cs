using OfferwallApi.Shared.Entities;

namespace OfferwallApi.Entities;

public sealed class User
{
    public Guid UserId { get; set; }

    public Guid PartnerId { get; set; }

    public string ExternalUserId { get; set; } = null!;

    public int Xp { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Partner Partner { get; set; } = null!;

    public Wallet Wallet { get; set; } = null!;

    public ICollection<Conversion> Conversions { get; set; } = [];

    public ICollection<Coupon> Coupons { get; set; } = [];

    public ICollection<SupportTicket> SupportTickets { get; set; } = [];

    public ICollection<WalletTransaction> WalletTransactions { get; set; } = [];

    public ICollection<Click> Clicks { get; set; } = [];
}
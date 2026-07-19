namespace OfferwallApi.Entities;

public sealed class Wallet
{
    public Guid UserId { get; set; }

    public decimal Balance { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public User User { get; set; } = null!;
}
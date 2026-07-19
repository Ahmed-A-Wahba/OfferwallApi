namespace OfferwallApi.Entities;

public sealed class OfferCache
{
    public string OfferId { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string Logo { get; set; } = null!;

    public decimal TotalReward { get; set; }

    public int TotalTasks { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }
}
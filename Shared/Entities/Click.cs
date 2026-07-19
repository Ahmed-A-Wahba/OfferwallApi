namespace OfferwallApi.Entities;

public sealed class Click
{
    public Guid ClickId { get; set; }

    public Guid UserId { get; set; }

    public string OfferId { get; set; } = null!;

    public ClickStatus Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public User User { get; set; } = null!;
}

public enum ClickStatus
{
    Pending = 1,
    Completed = 2,
}
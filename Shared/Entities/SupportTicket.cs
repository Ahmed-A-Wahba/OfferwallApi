namespace OfferwallApi.Shared.Entities;

public sealed class SupportTicket
{
    public Guid TicketId { get; set; }

    public Guid UserId { get; set; }

    public TicketCategory Category { get; set; }

    public string Subject { get; set; } = null!;

    public string Description { get; set; } = null!;

    public string? ScreenshotUrl { get; set; }

    public TicketStatus Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public User User { get; set; } = null!;
}


public enum TicketCategory
{
    Offer = 1,
    Coupon = 2,
    Wallet = 3,
    Payment = 4,
    Other = 5
}

public enum TicketStatus
{
    Open = 1,
    InProgress = 2,
    Closed = 3
}
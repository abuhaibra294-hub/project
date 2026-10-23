namespace DareCare.Domain.Entities;

public sealed class SupportTicket
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid OwnerUserId { get; private set; }
    public string Subject { get; private set; } = string.Empty;
    public SupportTicketCategory Category { get; private set; }
    public SupportTicketStatus Status { get; private set; } = SupportTicketStatus.Open;
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; private set; } = DateTime.UtcNow;
    public Guid Version { get; private set; } = Guid.NewGuid();
    public List<SupportMessage> Messages { get; private set; } = [];

    private SupportTicket() { } // EF Core

    public static SupportTicket Create(Guid ownerId, SupportTicketCategory category, string subject)
    {
        if (ownerId == Guid.Empty) throw new ArgumentException("Owner is required.", nameof(ownerId));
        if (!Enum.IsDefined(category)) throw new ArgumentOutOfRangeException(nameof(category));
        subject = subject?.Trim() ?? "";
        if (subject.Length is < 5 or > 160)
            throw new ArgumentException("Subject must contain 5-160 characters.", nameof(subject));
        return new SupportTicket { OwnerUserId = ownerId, Category = category, Subject = subject };
    }

    public void ChangeStatus(SupportTicketStatus next)
    {
        if (!Enum.IsDefined(next)) throw new ArgumentOutOfRangeException(nameof(next));
        if (Status == next) return;
        var allowed = (Status, next) switch
        {
            (SupportTicketStatus.Open, SupportTicketStatus.InProgress) => true,
            (SupportTicketStatus.Open, SupportTicketStatus.Resolved) => true,
            (SupportTicketStatus.Open, SupportTicketStatus.Closed) => true,
            (SupportTicketStatus.InProgress, SupportTicketStatus.Resolved) => true,
            (SupportTicketStatus.InProgress, SupportTicketStatus.Closed) => true,
            (SupportTicketStatus.Resolved, SupportTicketStatus.InProgress) => true,
            (SupportTicketStatus.Resolved, SupportTicketStatus.Closed) => true,
            _ => false
        };
        if (!allowed) throw new InvalidOperationException($"Unsupported status change: {Status} -> {next}.");
        Status = next;
        Touch();
    }

    public void Touch()
    {
        UpdatedAtUtc = DateTime.UtcNow;
        Version = Guid.NewGuid();
    }
}

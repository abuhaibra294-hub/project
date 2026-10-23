namespace DareCare.Domain.Entities;

public sealed class SupportMessage
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid SupportTicketId { get; private set; }
    public Guid SenderUserId { get; private set; }
    public string Body { get; private set; } = string.Empty;
    public bool IsStaffReply { get; private set; }
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;

    private SupportMessage() { } // EF Core

    public static SupportMessage Create(Guid ticketId, Guid senderUserId, string body, bool isStaffReply)
    {
        if (ticketId == Guid.Empty || senderUserId == Guid.Empty)
            throw new ArgumentException("Ticket and sender ids are required.");
        body = body?.Trim() ?? "";
        if (body.Length is < 1 or > 4000)
            throw new ArgumentException("Message must contain 1-4000 characters.", nameof(body));
        return new SupportMessage
        {
            SupportTicketId = ticketId, SenderUserId = senderUserId,
            Body = body, IsStaffReply = isStaffReply
        };
    }
}

namespace DareCare.Infrastructure.Identity;

// One refresh token per rotation. Store only the SHA-256 hash, never the plaintext.
public sealed class RefreshSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;
    public Guid FamilyId { get; set; } = Guid.NewGuid();
    // Prevents refresh/logout-all races from resurrecting an old session.
    public int TokenVersion { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public string? ReplacedByHash { get; set; }
}

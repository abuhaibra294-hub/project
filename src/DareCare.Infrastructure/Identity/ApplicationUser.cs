using Microsoft.AspNetCore.Identity;

namespace DareCare.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string FullName { get; set; } = string.Empty;
    public DateTime RegisteredAtUtc { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
    // Checked against the access token on every authenticated request.
    public int TokenVersion { get; set; }
    // Provisioned pharmacy staff must first use their email invitation to set a password.
    public bool IsPendingInvitation { get; set; }
}

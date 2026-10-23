namespace DareCare.Domain.Entities;

// Private metadata only: the actual document is kept outside wwwroot.
public sealed class Prescription
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PatientUserId { get; set; }
    public Guid PharmacyProfileId { get; set; }
    public PharmacyProfile Pharmacy { get; set; } = null!;
    public Guid? BookingId { get; set; }
    public string? PatientNotes { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string StorageKey { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

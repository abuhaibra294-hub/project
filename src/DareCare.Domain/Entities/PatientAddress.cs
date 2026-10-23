namespace DareCare.Domain.Entities;

// A saved address is private to one patient. Bookings store an immutable address snapshot.
public sealed class PatientAddress
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PatientUserId { get; set; }
    public string Label { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string FullAddress { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public bool IsDefault { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public string BookingAddress => $"{City} - {FullAddress}";
}

namespace DareCare.Domain.Entities;

public sealed class PharmacyOrder
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PrescriptionId { get; set; }
    public Prescription Prescription { get; set; } = null!;
    public Guid PatientUserId { get; set; }
    public Guid PharmacyProfileId { get; set; }
    public decimal QuoteAmount { get; set; }
    public string? QuoteNotes { get; set; }
    public PharmacyOrderStatus Status { get; private set; } = PharmacyOrderStatus.Quoted;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; private set; } = DateTime.UtcNow;
    // Works as a concurrency token on both SQL Server and SQLite.
    public Guid Version { get; private set; } = Guid.NewGuid();

    public void TransitionTo(PharmacyOrderStatus next)
    {
        var valid = (Status, next) switch
        {
            (PharmacyOrderStatus.Quoted, PharmacyOrderStatus.Confirmed) => true,
            (PharmacyOrderStatus.Quoted, PharmacyOrderStatus.Declined) => true,
            (PharmacyOrderStatus.Confirmed, PharmacyOrderStatus.Preparing) => true,
            (PharmacyOrderStatus.Preparing, PharmacyOrderStatus.OutForDelivery) => true,
            (PharmacyOrderStatus.OutForDelivery, PharmacyOrderStatus.Delivered) => true,
            _ => false
        };
        if (!valid) throw new InvalidOperationException($"Invalid order transition: {Status} -> {next}.");
        Status = next;
        UpdatedAtUtc = DateTime.UtcNow;
        Version = Guid.NewGuid();
    }
}

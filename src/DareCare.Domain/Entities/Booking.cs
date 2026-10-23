namespace DareCare.Domain.Entities;

public sealed class Booking
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PatientUserId { get; set; }
    public Guid NurseProfileId { get; set; }
    public NurseProfile Nurse { get; set; } = null!;
    public Guid AvailabilitySlotId { get; set; }
    public AvailabilitySlot Slot { get; set; } = null!;
    public Guid CareServiceId { get; set; }
    public CareService Service { get; set; } = null!;
    public string ServiceAddress { get; set; } = string.Empty;
    public decimal PriceAtBooking { get; set; }
    public BookingStatus Status { get; private set; } = BookingStatus.Pending;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; private set; } = DateTime.UtcNow;
    public byte[] RowVersion { get; set; } = [];
    public Guid Version { get; private set; } = Guid.NewGuid(); // SQLite/SQL Server concurrency

    // No skip between states: protects the nurse/patient booking workflow.
    public void TransitionTo(BookingStatus next)
    {
        var isAllowed = (Status, next) switch
        {
            (BookingStatus.Pending, BookingStatus.Accepted) => true,
            (BookingStatus.Pending, BookingStatus.Rejected) => true,
            (BookingStatus.Pending, BookingStatus.Cancelled) => true,
            (BookingStatus.Accepted, BookingStatus.InProgress) => true,
            (BookingStatus.Accepted, BookingStatus.Cancelled) => true,
            (BookingStatus.InProgress, BookingStatus.Completed) => true,
            _ => false
        };
        if (!isAllowed)
            throw new InvalidOperationException($"Invalid booking transition: {Status} -> {next}.");

        Status = next;
        UpdatedAtUtc = DateTime.UtcNow;
        Version = Guid.NewGuid();
    }
}

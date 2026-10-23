namespace DareCare.Domain.Entities;

public sealed class AvailabilitySlot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid NurseProfileId { get; set; }
    public NurseProfile Nurse { get; set; } = null!;
    public DateTime StartsAtUtc { get; set; }
    public DateTime EndsAtUtc { get; set; }
}

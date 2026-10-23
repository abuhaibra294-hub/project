namespace DareCare.Domain.Entities;

public sealed class FavoriteNurse
{
    public Guid PatientUserId { get; set; }
    public Guid NurseProfileId { get; set; }
    public NurseProfile Nurse { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

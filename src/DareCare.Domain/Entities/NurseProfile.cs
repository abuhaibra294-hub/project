namespace DareCare.Domain.Entities;

public sealed class NurseProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string LicenseNumber { get; set; } = string.Empty;
    public string? Bio { get; set; }
    public string? City { get; set; }
    public string? ServiceArea { get; set; }
    public int YearsOfExperience { get; set; }
    public decimal HourlyRate { get; set; }
    public bool IsApproved { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public List<NurseSkill> NurseSkills { get; set; } = [];
    public List<AvailabilitySlot> AvailabilitySlots { get; set; } = [];
}

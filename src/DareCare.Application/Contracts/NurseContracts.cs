using System.ComponentModel.DataAnnotations;

namespace DareCare.Application.Contracts;

public sealed class UpdateNurseProfileRequest
{
    [StringLength(1500)]
    public string? Bio { get; set; }
    [StringLength(120)]
    public string? City { get; set; }
    [StringLength(250)]
    public string? ServiceArea { get; set; }
    [Range(0, 60)]
    public int YearsOfExperience { get; set; }
    [Range(typeof(decimal), "0", "100000")]
    public decimal HourlyRate { get; set; }
}

public sealed class SetNurseSkillsRequest
{
    [Required, MinLength(1)]
    public List<Guid> SkillIds { get; set; } = [];
}

public sealed class CreateAvailabilitySlotRequest
{
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
}

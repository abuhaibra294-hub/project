namespace DareCare.Domain.Entities;

public sealed class CareService
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
    public decimal BasePrice { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid RequiredSkillId { get; set; }
    public Skill RequiredSkill { get; set; } = null!;
}

namespace DareCare.Domain.Entities;

public sealed class NurseSkill
{
    public Guid NurseProfileId { get; set; }
    public NurseProfile Nurse { get; set; } = null!;
    public Guid SkillId { get; set; }
    public Skill Skill { get; set; } = null!;
}

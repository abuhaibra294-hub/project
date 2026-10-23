using DareCare.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DareCare.Api.Controllers;

[ApiController]
[Route("api/v1")]
public sealed class ServicesController(DareCareDbContext db) : ControllerBase
{
    [HttpGet("skills")]
    public async Task<IActionResult> Skills(CancellationToken ct)
    {
        var skills = await db.Skills.AsNoTracking().OrderBy(x => x.Name)
            .Select(x => new { x.Id, x.Name }).ToListAsync(ct);
        return Ok(skills);
    }

    [HttpGet("services/search")]
    public async Task<IActionResult> SearchServices([FromQuery] string? query,
        [FromQuery] Guid? skillId, [FromQuery] decimal? maxPrice,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        if (query?.Length > 100 || skillId == Guid.Empty ||
            (maxPrice.HasValue && maxPrice.Value < 0) ||
            page is < 1 or > 100000 || pageSize is < 1 or > 50)
            return BadRequest(new { message = "Invalid service search filters or pagination." });
        var source = db.CareServices.AsNoTracking().Where(s => s.IsActive);
        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim();
            source = source.Where(s => s.Name.Contains(term) || s.Description.Contains(term));
        }
        if (skillId.HasValue) source = source.Where(s => s.RequiredSkillId == skillId.Value);
        if (maxPrice.HasValue) source = source.Where(s => s.BasePrice <= maxPrice.Value);
        return Ok(new
        {
            page, pageSize, total = await source.CountAsync(ct),
            items = await source.OrderBy(s => s.Name).ThenBy(s => s.Id)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(s => new { s.Id, s.Name, s.Description, s.DurationMinutes,
                    s.BasePrice, s.RequiredSkillId, RequiredSkill = s.RequiredSkill.Name })
                .ToListAsync(ct)
        });
    }

    [HttpGet("services")]
    public async Task<IActionResult> Services(CancellationToken ct)
    {
        var services = await db.CareServices.AsNoTracking().Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new { x.Id, x.Name, x.Description, x.DurationMinutes, x.BasePrice,
                x.RequiredSkillId, RequiredSkill = x.RequiredSkill.Name }).ToListAsync(ct);
        return Ok(services);
    }
}

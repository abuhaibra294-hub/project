using System.Data;
using DareCare.Api.Security;
using DareCare.Application.Contracts;
using DareCare.Domain;
using DareCare.Domain.Entities;
using DareCare.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DareCare.Api.Controllers;

[ApiController]
[Route("api/v1/nurses")]
public sealed class NursesController(DareCareDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> ListApproved(CancellationToken ct)
    {
        // The public endpoint never leaks a license number or unapproved profile.
        var nurses = await db.Nurses.AsNoTracking().Where(x => x.IsApproved &&
                db.Users.Any(u => u.Id == x.UserId && u.IsActive && !u.IsPendingInvitation))
            .OrderBy(x => x.CreatedAtUtc).Take(50)
            .Select(x => new
            {
                x.Id, x.Bio, x.City, x.ServiceArea, x.YearsOfExperience, x.HourlyRate,
                FullName = db.Users.Where(u => u.Id == x.UserId).Select(u => u.FullName).FirstOrDefault(),
                Skills = x.NurseSkills.Select(s => new { s.SkillId, s.Skill.Name }).ToList()
            }).ToListAsync(ct);
        return Ok(nurses);
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string? city, [FromQuery] Guid? skillId,
        [FromQuery] int? minYears, [FromQuery] decimal? maxHourlyRate,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        if (page is < 1 or > 100000 || pageSize is < 1 or > 50 ||
            city?.Length > 120 || (minYears.HasValue && minYears.Value is < 0 or > 60) ||
            (maxHourlyRate.HasValue && maxHourlyRate.Value < 0) || skillId == Guid.Empty)
            return BadRequest(new { message = "Invalid nurse search filters or pagination." });
        var query = db.Nurses.AsNoTracking().Where(n => n.IsApproved &&
            db.Users.Any(u => u.Id == n.UserId && u.IsActive && !u.IsPendingInvitation));
        if (!string.IsNullOrWhiteSpace(city))
        {
            var term = city.Trim();
            query = query.Where(n => n.City != null && n.City.Contains(term));
        }
        if (skillId.HasValue) query = query.Where(n => n.NurseSkills.Any(s => s.SkillId == skillId.Value));
        if (minYears.HasValue) query = query.Where(n => n.YearsOfExperience >= minYears.Value);
        if (maxHourlyRate.HasValue) query = query.Where(n => n.HourlyRate <= maxHourlyRate.Value);
        return Ok(new
        {
            page, pageSize, total = await query.CountAsync(ct),
            items = await query.OrderByDescending(n => n.YearsOfExperience).ThenBy(n => n.Id)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(n => new
                {
                    n.Id, n.Bio, n.City, n.ServiceArea, n.YearsOfExperience, n.HourlyRate,
                    FullName = db.Users.Where(u => u.Id == n.UserId)
                        .Select(u => u.FullName).FirstOrDefault(),
                    Skills = n.NurseSkills.Select(s => new { s.SkillId, s.Skill.Name }).ToList()
                }).ToListAsync(ct)
        });
    }

    [HttpGet("{nurseId:guid}")]
    public async Task<IActionResult> PublicProfile(Guid nurseId, CancellationToken ct)
    {
        var profile = await db.Nurses.AsNoTracking().Where(n => n.Id == nurseId && n.IsApproved &&
            db.Users.Any(u => u.Id == n.UserId && u.IsActive && !u.IsPendingInvitation))
            .Select(n => new
            {
                n.Id, n.Bio, n.City, n.ServiceArea, n.YearsOfExperience, n.HourlyRate,
                FullName = db.Users.Where(u => u.Id == n.UserId).Select(u => u.FullName).FirstOrDefault(),
                Skills = n.NurseSkills.Select(s => new { s.SkillId, s.Skill.Name }).ToList()
            }).SingleOrDefaultAsync(ct);
        if (profile is null) return NotFound();
        var ratings = await db.NurseReviews.AsNoTracking().Where(r => r.NurseProfileId == nurseId)
            .GroupBy(r => r.NurseProfileId)
            .Select(g => new { Count = g.Count(), Average = g.Average(r => (double)r.Rating) })
            .SingleOrDefaultAsync(ct);
        return Ok(new { profile.Id, profile.Bio, profile.City, profile.ServiceArea,
            profile.YearsOfExperience, profile.HourlyRate, profile.FullName, profile.Skills,
            reviewCount = ratings?.Count ?? 0,
            averageRating = ratings is null ? (double?)null : Math.Round(ratings.Average, 1) });
    }

    [HttpGet("me/slots")]
    [Authorize(Roles = AppRoles.Nurse)]
    public async Task<IActionResult> MySlots([FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to, CancellationToken ct)
    {
        var nurseId = await db.Nurses.AsNoTracking()
            .Where(n => n.UserId == User.CurrentUserId()).Select(n => (Guid?)n.Id)
            .FirstOrDefaultAsync(ct);
        if (nurseId is null) return NotFound();
        var lower = from?.UtcDateTime ?? DateTime.UtcNow.AddDays(-1);
        var upper = to?.UtcDateTime ?? DateTime.UtcNow.AddDays(90);
        if (upper < lower || upper - lower > TimeSpan.FromDays(180))
            return BadRequest(new { message = "Date range must be ordered and within 180 days." });
        return Ok(await db.AvailabilitySlots.AsNoTracking()
            .Where(s => s.NurseProfileId == nurseId && s.StartsAtUtc >= lower && s.StartsAtUtc <= upper)
            .OrderBy(s => s.StartsAtUtc).Take(200)
            .Select(s => new
            {
                s.Id, s.StartsAtUtc, s.EndsAtUtc,
                IsBooked = db.Bookings.Any(b => b.AvailabilitySlotId == s.Id &&
                    b.Status != BookingStatus.Cancelled && b.Status != BookingStatus.Rejected)
            }).ToListAsync(ct));
    }

    [HttpGet("me")]
    [Authorize(Roles = AppRoles.Nurse)]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var id = User.CurrentUserId();
        var profile = await db.Nurses.AsNoTracking().Where(x => x.UserId == id)
            .Select(x => new { x.Id, x.LicenseNumber, x.Bio, x.City, x.ServiceArea, x.YearsOfExperience,
                x.HourlyRate, x.IsApproved,
                Skills = x.NurseSkills.Select(s => new { s.SkillId, s.Skill.Name }).ToList() })
            .FirstOrDefaultAsync(ct);
        return profile is null ? NotFound() : Ok(profile);
    }

    [HttpPut("me")]
    [Authorize(Roles = AppRoles.Nurse)]
    public async Task<IActionResult> UpdateMine([FromBody] UpdateNurseProfileRequest input, CancellationToken ct)
    {
        var id = User.CurrentUserId();
        var nurse = await db.Nurses.SingleOrDefaultAsync(x => x.UserId == id, ct);
        if (nurse is null) return NotFound();
        var city = input.City?.Trim();
        var serviceArea = input.ServiceArea?.Trim();
        if (nurse.IsApproved && (nurse.City != city || nurse.ServiceArea != serviceArea ||
                                 nurse.YearsOfExperience != input.YearsOfExperience))
            nurse.IsApproved = false; // Reapprove any material professional/location change.
        nurse.Bio = input.Bio?.Trim();
        nurse.City = city;
        nurse.ServiceArea = serviceArea;
        nurse.YearsOfExperience = input.YearsOfExperience;
        nurse.HourlyRate = input.HourlyRate;
        await db.SaveChangesAsync(ct);
        return Ok(new { nurse.Id, nurse.Bio, nurse.City, nurse.ServiceArea, nurse.YearsOfExperience, nurse.HourlyRate, nurse.IsApproved });
    }

    [HttpPut("me/skills")]
    [Authorize(Roles = AppRoles.Nurse)]
    public async Task<IActionResult> SetSkills([FromBody] SetNurseSkillsRequest input, CancellationToken ct)
    {
        var userId = User.CurrentUserId();
        var nurse = await db.Nurses.SingleOrDefaultAsync(x => x.UserId == userId, ct);
        if (nurse is null) return NotFound();
        var selected = input.SkillIds.Distinct().ToHashSet();
        if (selected.Count == 0 || selected.Contains(Guid.Empty))
            return BadRequest(new { message = "Select one or more valid skills." });
        var exists = await db.Skills.CountAsync(x => selected.Contains(x.Id), ct);
        if (exists != selected.Count) return BadRequest(new { message = "Unknown skill id." });

        var current = await db.NurseSkills.Where(x => x.NurseProfileId == nurse.Id).ToListAsync(ct);
        var old = current.Select(x => x.SkillId).ToHashSet();
        if (!old.SetEquals(selected))
        {
            db.NurseSkills.RemoveRange(current.Where(x => !selected.Contains(x.SkillId)));
            db.NurseSkills.AddRange(selected.Except(old).Select(skillId =>
                new NurseSkill { NurseProfileId = nurse.Id, SkillId = skillId }));
            // A changed skill set requires new human approval.
            nurse.IsApproved = false;
            await db.SaveChangesAsync(ct);
        }
        return Ok(new { nurse.Id, nurse.IsApproved, SkillIds = selected.ToArray() });
    }

    [HttpPost("me/slots")]
    [Authorize(Roles = AppRoles.Nurse)]
    public async Task<IActionResult> AddSlot([FromBody] CreateAvailabilitySlotRequest input, CancellationToken ct)
    {
        var userId = User.CurrentUserId();
        var nurse = await db.Nurses.SingleOrDefaultAsync(x => x.UserId == userId, ct);
        if (nurse is null) return NotFound();
        var start = input.StartsAt.UtcDateTime;
        var end = input.EndsAt.UtcDateTime;
        var now = DateTime.UtcNow;
        if (start < now.AddMinutes(10) || start > now.AddDays(90) ||
            end <= start || end - start > TimeSpan.FromHours(24))
            return BadRequest(new { message = "Slot must start 10 minutes to 90 days in the future, with 0-24 hour duration." });
        // An overlap check without a transaction is not sufficient: concurrent
        // requests could each observe an empty window and insert overlapping slots.
        // SQL Server holds serializable key-range locks; SQLite serializes writers.
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var overlaps = await db.AvailabilitySlots.AnyAsync(x =>
                x.NurseProfileId == nurse.Id && x.StartsAtUtc < end && x.EndsAtUtc > start, ct);
            if (overlaps) return Conflict(new { message = "Slot overlaps an existing availability window." });

            var slot = new AvailabilitySlot { NurseProfileId = nurse.Id, StartsAtUtc = start, EndsAtUtc = end };
            db.AvailabilitySlots.Add(slot);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Created($"/api/v1/nurses/{nurse.Id}/slots", new { slot.Id, slot.StartsAtUtc, slot.EndsAtUtc });
        }
        catch (DbUpdateException ex) when (DbConflict.IsUniqueViolation(ex))
        {
            return Conflict(new { message = "An availability window was created concurrently. Reload and retry." });
        }
        catch (Exception ex) when (DbConflict.IsRetryableWriteConflict(ex))
        {
            return Conflict(new { message = "Availability changed concurrently. Reload and retry." });
        }
    }

    [HttpGet("{nurseId:guid}/slots")]
    public async Task<IActionResult> AvailableSlots(Guid nurseId, CancellationToken ct)
    {
        var approved = await db.Nurses.AnyAsync(x => x.Id == nurseId && x.IsApproved &&
            db.Users.Any(u => u.Id == x.UserId && u.IsActive && !u.IsPendingInvitation), ct);
        if (!approved) return NotFound();
        var now = DateTime.UtcNow;
        var slots = await db.AvailabilitySlots.AsNoTracking()
            .Where(s => s.NurseProfileId == nurseId && s.StartsAtUtc > now)
            .Where(s => !db.Bookings.Any(b => b.AvailabilitySlotId == s.Id &&
                b.Status != BookingStatus.Cancelled && b.Status != BookingStatus.Rejected))
            .OrderBy(s => s.StartsAtUtc)
            .Select(s => new { s.Id, s.StartsAtUtc, s.EndsAtUtc })
            .Take(100).ToListAsync(ct);
        return Ok(slots);
    }

    [HttpDelete("me/slots/{slotId:guid}")]
    [Authorize(Roles = AppRoles.Nurse)]
    public async Task<IActionResult> DeleteSlot(Guid slotId, CancellationToken ct)
    {
        var id = User.CurrentUserId();
        var slot = await db.AvailabilitySlots.Include(x => x.Nurse)
            .FirstOrDefaultAsync(x => x.Id == slotId, ct);
        if (slot is null) return NotFound();
        if (slot.Nurse.UserId != id) return Forbid();
        if (await db.Bookings.AnyAsync(x => x.AvailabilitySlotId == slot.Id, ct))
            return Conflict(new { message = "A slot with booking history cannot be deleted." });
        db.AvailabilitySlots.Remove(slot);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}

using DareCare.Api.Security;
using DareCare.Domain;
using DareCare.Domain.Entities;
using DareCare.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DareCare.Api.Controllers;

[ApiController]
[Route("api/v1/favorites/nurses")]
[Authorize(Roles = AppRoles.Patient)]
public sealed class FavoriteNursesController(DareCareDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> All(CancellationToken ct)
    {
        var userId = User.CurrentUserId();
        var items = await db.FavoriteNurses.AsNoTracking()
            .Where(f => f.PatientUserId == userId && f.Nurse.IsApproved &&
                db.Users.Any(u => u.Id == f.Nurse.UserId && u.IsActive && !u.IsPendingInvitation))
            .OrderByDescending(f => f.CreatedAtUtc)
            .Select(f => new
            {
                f.NurseProfileId, f.CreatedAtUtc, f.Nurse.City, f.Nurse.ServiceArea,
                f.Nurse.YearsOfExperience, f.Nurse.HourlyRate,
                FullName = db.Users.Where(u => u.Id == f.Nurse.UserId)
                    .Select(u => u.FullName).FirstOrDefault()
            }).ToListAsync(ct);
        return Ok(items);
    }

    [HttpGet("{nurseId:guid}")]
    public async Task<IActionResult> IsFavorite(Guid nurseId, CancellationToken ct)
    {
        var userId = User.CurrentUserId();
        return Ok(new { nurseProfileId = nurseId, isFavorite = await db.FavoriteNurses
            .AnyAsync(x => x.PatientUserId == userId && x.NurseProfileId == nurseId, ct) });
    }

    [HttpPost("{nurseId:guid}")]
    public async Task<IActionResult> Add(Guid nurseId, CancellationToken ct)
    {
        var userId = User.CurrentUserId();
        var eligible = await db.Nurses.AsNoTracking().AnyAsync(n => n.Id == nurseId && n.IsApproved &&
            db.Users.Any(u => u.Id == n.UserId && u.IsActive && !u.IsPendingInvitation), ct);
        if (!eligible) return NotFound(new { message = "Approved nurse not found." });
        if (await db.FavoriteNurses.AnyAsync(f => f.PatientUserId == userId && f.NurseProfileId == nurseId, ct))
            return NoContent();
        db.FavoriteNurses.Add(new FavoriteNurse { PatientUserId = userId, NurseProfileId = nurseId });
        db.AuditEvents.Add(new AuditEvent { ActorUserId = userId, Action = "favorite.added",
            EntityType = "NurseProfile", EntityId = nurseId.ToString() });
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (DbConflict.IsUniqueViolation(ex)) { return NoContent(); }
        return Created($"/api/v1/favorites/nurses/{nurseId}", new { nurseProfileId = nurseId });
    }

    [HttpDelete("{nurseId:guid}")]
    public async Task<IActionResult> Remove(Guid nurseId, CancellationToken ct)
    {
        var userId = User.CurrentUserId();
        var favorite = await db.FavoriteNurses.SingleOrDefaultAsync(f =>
            f.PatientUserId == userId && f.NurseProfileId == nurseId, ct);
        if (favorite is null) return NoContent(); // Idempotent and scoped to owner.
        db.FavoriteNurses.Remove(favorite);
        db.AuditEvents.Add(new AuditEvent { ActorUserId = userId, Action = "favorite.removed",
            EntityType = "NurseProfile", EntityId = nurseId.ToString() });
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}

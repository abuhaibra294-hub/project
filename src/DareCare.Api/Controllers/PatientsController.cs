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
[Route("api/v1/patients")]
[Authorize(Roles = AppRoles.Patient)]
public sealed class PatientsController(DareCareDbContext db) : ControllerBase
{
    [HttpGet("me")]
    public async Task<IActionResult> GetMine(CancellationToken ct)
    {
        var uid = User.CurrentUserId();
        var profile = await db.Patients.AsNoTracking().Where(x => x.UserId == uid)
            .Select(x => new { x.Id, x.City, x.DefaultAddress, x.EmergencyContactName,
                x.EmergencyContactPhone, x.CreatedAtUtc }).SingleOrDefaultAsync(ct);
        return profile is null ? NotFound() : Ok(profile);
    }

    [HttpPut("me")]
    public async Task<IActionResult> UpdateMine([FromBody] UpdatePatientProfileRequest input, CancellationToken ct)
    {
        var uid = User.CurrentUserId();
        var profile = await db.Patients.SingleOrDefaultAsync(x => x.UserId == uid, ct);
        if (profile is null) return NotFound();
        profile.City = input.City?.Trim();
        profile.DefaultAddress = input.DefaultAddress?.Trim();
        profile.EmergencyContactName = input.EmergencyContactName?.Trim();
        profile.EmergencyContactPhone = input.EmergencyContactPhone?.Trim();
        db.AuditEvents.Add(new AuditEvent
        {
            ActorUserId = uid, Action = "patient.profile.updated",
            EntityType = "PatientProfile", EntityId = profile.Id.ToString()
        });
        await db.SaveChangesAsync(ct);
        return Ok(new { profile.Id, profile.City, profile.DefaultAddress,
            profile.EmergencyContactName, profile.EmergencyContactPhone });
    }
}

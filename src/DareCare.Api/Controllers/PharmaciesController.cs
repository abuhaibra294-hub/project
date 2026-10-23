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
[Route("api/v1/pharmacies")]
[Authorize(Roles = AppRoles.Pharmacy)]
public sealed class PharmaciesController(DareCareDbContext db) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("available")]
    public async Task<IActionResult> Available(CancellationToken ct)
    {
        var items = await db.Pharmacies.AsNoTracking()
            .Where(p => p.IsApproved && db.Users.Any(u => u.Id == p.UserId && u.IsActive && !u.IsPendingInvitation))
            .OrderBy(p => p.BusinessName)
            .Select(p => new { p.Id, p.BusinessName, p.City, p.Address })
            .Take(100).ToListAsync(ct);
        return Ok(items);
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetMine(CancellationToken ct)
    {
        var uid = User.CurrentUserId();
        var profile = await db.Pharmacies.AsNoTracking().Where(x => x.UserId == uid)
            .Select(x => new { x.Id, x.BusinessName, x.LicenseNumber,
                x.City, x.Address, x.IsApproved, x.CreatedAtUtc }).SingleOrDefaultAsync(ct);
        return profile is null ? NotFound() : Ok(profile);
    }

    [HttpPut("me")]
    public async Task<IActionResult> UpdateMine([FromBody] UpdatePharmacyRequest input, CancellationToken ct)
    {
        var uid = User.CurrentUserId();
        var profile = await db.Pharmacies.SingleOrDefaultAsync(x => x.UserId == uid, ct);
        if (profile is null) return NotFound();
        var changed = profile.BusinessName != input.BusinessName.Trim() ||
            profile.City != input.City.Trim() || profile.Address != input.Address.Trim();
        profile.BusinessName = input.BusinessName.Trim();
        profile.City = input.City.Trim();
        profile.Address = input.Address.Trim();
        if (changed) profile.IsApproved = false; // Owner details must be reviewed again.
        if (changed) db.AuditEvents.Add(new AuditEvent
        {
            ActorUserId = uid, Action = "pharmacy.profile.updated",
            EntityType = "PharmacyProfile", EntityId = profile.Id.ToString()
        });
        await db.SaveChangesAsync(ct);
        return Ok(new { profile.Id, profile.BusinessName, profile.City, profile.Address, profile.IsApproved });
    }
}

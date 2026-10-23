using DareCare.Api.Security;
using DareCare.Api.Services;
using DareCare.Application.Contracts;
using DareCare.Domain;
using DareCare.Domain.Entities;
using DareCare.Infrastructure.Identity;
using DareCare.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace DareCare.Api.Controllers;

[ApiController]
[Route("api/v1/admin")]
[Authorize(Roles = AppRoles.Admin)]
public sealed class AdminController(
    DareCareDbContext db,
    UserManager<ApplicationUser> users,
    JwtTokenService tokens,
    IAccountEmailService emails,
    ILogger<AdminController> logger) : ControllerBase
{
    [HttpGet("overview")]
    public async Task<IActionResult> Overview(CancellationToken ct)
    {
        var activePatients = await db.Patients.CountAsync(p => db.Users.Any(u => u.Id == p.UserId && u.IsActive), ct);
        var nursesPending = await db.Nurses.CountAsync(n => !n.IsApproved, ct);
        var nursesApproved = await db.Nurses.CountAsync(n => n.IsApproved, ct);
        var pharmaciesPending = await db.Pharmacies.CountAsync(p => !p.IsApproved, ct);
        var pharmaciesApproved = await db.Pharmacies.CountAsync(p => p.IsApproved, ct);
        var bookings = await db.Bookings.GroupBy(b => b.Status)
            .Select(g => new { status = g.Key, count = g.Count() }).ToListAsync(ct);
        var orders = await db.PharmacyOrders.GroupBy(o => o.Status)
            .Select(g => new { status = g.Key, count = g.Count() }).ToListAsync(ct);
        return Ok(new { activePatients, nursesPending, nursesApproved,
            pharmaciesPending, pharmaciesApproved,
            prescriptions = await db.Prescriptions.CountAsync(ct),
            completedReviews = await db.NurseReviews.CountAsync(ct), bookings, orders });
    }

    [HttpGet("nurses/pending")]
    public async Task<IActionResult> PendingNurses(CancellationToken ct)
    {
        var list = await db.Nurses.AsNoTracking().Where(x => !x.IsApproved)
            .OrderBy(x => x.CreatedAtUtc).Take(100)
            .Select(x => new { x.Id, x.UserId, x.LicenseNumber, x.YearsOfExperience,
                x.CreatedAtUtc, x.City, x.ServiceArea,
                FullName = db.Users.Where(u => u.Id == x.UserId).Select(u => u.FullName).FirstOrDefault(),
                Email = db.Users.Where(u => u.Id == x.UserId).Select(u => u.Email).FirstOrDefault(),
                Skills = x.NurseSkills.Select(s => s.Skill.Name).ToList() })
            .ToListAsync(ct);
        return Ok(list);
    }

    [HttpPatch("nurses/{nurseId:guid}/approval")]
    public async Task<IActionResult> SetApproval(Guid nurseId, [FromBody] SetApprovalRequest input, CancellationToken ct)
    {
        var approve = input.Approved!.Value;
        var nurse = await db.Nurses.FirstOrDefaultAsync(x => x.Id == nurseId, ct);
        if (nurse is null) return NotFound();
        if (approve && !await db.NurseSkills.AnyAsync(x => x.NurseProfileId == nurseId, ct))
            return BadRequest(new { message = "Nurse must declare a skill before approval." });
        if (nurse.IsApproved == approve) return Ok(new { nurse.Id, nurse.IsApproved });
        nurse.IsApproved = approve;
        db.Notifications.Add(new UserNotification
        {
            RecipientUserId = nurse.UserId, Title = "Nurse approval updated",
            Message = approve ? "Your nurse profile has been approved." : "Your nurse profile approval has been withdrawn.",
            ReferenceType = "NurseProfile", ReferenceId = nurse.Id
        });
        Audit(approve ? "nurse.approved" : "nurse.rejected", "NurseProfile", nurse.Id.ToString());
        await db.SaveChangesAsync(ct);
        return Ok(new { nurse.Id, nurse.IsApproved });
    }

    // Pharmacy registration is ADMIN-ONLY, and the invited pharmacist sets their own password by email.
    [HttpPost("pharmacies")]
    public async Task<IActionResult> CreatePharmacy([FromBody] CreatePharmacyRequest input, CancellationToken ct)
    {
        if (!emails.IsConfigured) return StatusCode(503, new { message = "Configure SMTP for pharmacy invitations." });
        var license = input.LicenseNumber.Trim();
        if (await db.Pharmacies.AnyAsync(p => p.LicenseNumber == license, ct))
            return Conflict(new { message = "A pharmacy with this license already exists." });

        var account = new ApplicationUser
        {
            Email = input.Email.Trim().ToLowerInvariant(),
            UserName = input.Email.Trim().ToLowerInvariant(),
            FullName = input.ContactName.Trim(),
            PhoneNumber = input.PhoneNumber.Trim(),
            IsActive = false,
            IsPendingInvitation = true
        };
        var profile = new PharmacyProfile
        {
            UserId = account.Id,
            BusinessName = input.BusinessName.Trim(),
            LicenseNumber = license,
            City = input.City.Trim(),
            Address = input.Address.Trim(),
            IsApproved = false
        };

        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            var created = await users.CreateAsync(account); // No initial password is stored or shared by an admin.
            if (!created.Succeeded) return BadRequest(new { errors = created.Errors.Select(e => e.Description) });
            var role = await users.AddToRoleAsync(account, AppRoles.Pharmacy);
            if (!role.Succeeded) return Problem("Could not assign Pharmacy role.", statusCode: 500);
            db.Pharmacies.Add(profile);
            Audit("pharmacy.invited", "PharmacyProfile", profile.Id.ToString());
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException ex) when (DbConflict.IsUniqueViolation(ex))
            {
                return Conflict(new { message = "A pharmacy with this license already exists." });
            }
            await tx.CommitAsync(ct);
        }
        try { await emails.SendPasswordResetAsync(account, ct); }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unable to deliver pharmacy invite {PharmacyId}.", profile.Id);
            return StatusCode(502, new { profile.Id, message = "Pharmacy created, but invitation email failed. Use resend-invite." });
        }
        return CreatedAtAction(nameof(GetPharmacy),
            new { pharmacyId = profile.Id },
            new { profile.Id, profile.BusinessName, profile.IsApproved, invitationSent = true });
    }

    [HttpPost("pharmacies/{pharmacyId:guid}/resend-invite")]
    public async Task<IActionResult> ResendPharmacyInvite(Guid pharmacyId, CancellationToken ct)
    {
        if (!emails.IsConfigured) return StatusCode(503, new { message = "SMTP is not configured." });
        var userId = await db.Pharmacies.Where(p => p.Id == pharmacyId).Select(p => (Guid?)p.UserId)
            .SingleOrDefaultAsync(ct);
        if (userId is null) return NotFound();
        var account = await users.FindByIdAsync(userId.Value.ToString());
        if (account is null || !account.IsPendingInvitation) return Conflict(new { message = "Invitation already completed." });
        await emails.SendPasswordResetAsync(account, ct);
        return Accepted(new { message = "Invitation sent." });
    }

    [HttpGet("pharmacies/{pharmacyId:guid}")]
    public async Task<IActionResult> GetPharmacy(Guid pharmacyId, CancellationToken ct)
    {
        var profile = await db.Pharmacies.AsNoTracking().Where(x => x.Id == pharmacyId)
            .Select(x => new { x.Id, x.BusinessName, x.UserId, x.LicenseNumber,
                x.City, x.Address, x.IsApproved, x.CreatedAtUtc }).SingleOrDefaultAsync(ct);
        return profile is null ? NotFound() : Ok(profile);
    }

    [HttpGet("pharmacies/pending")]
    public async Task<IActionResult> PendingPharmacies(CancellationToken ct)
    {
        var list = await db.Pharmacies.AsNoTracking().Where(p => !p.IsApproved)
            .OrderBy(p => p.CreatedAtUtc).Take(100)
            .Select(p => new { p.Id, p.UserId, p.BusinessName, p.LicenseNumber,
                p.City, p.Address, p.CreatedAtUtc,
                Email = db.Users.Where(u => u.Id == p.UserId).Select(u => u.Email).FirstOrDefault(),
                HasAcceptedInvitation = db.Users.Where(u => u.Id == p.UserId).Select(u => !u.IsPendingInvitation).FirstOrDefault() })
            .ToListAsync(ct);
        return Ok(list);
    }

    [HttpPatch("pharmacies/{pharmacyId:guid}/approval")]
    public async Task<IActionResult> SetPharmacyApproval(Guid pharmacyId, [FromBody] SetApprovalRequest input, CancellationToken ct)
    {
        var pharmacy = await db.Pharmacies.FirstOrDefaultAsync(p => p.Id == pharmacyId, ct);
        if (pharmacy is null) return NotFound();
        var account = await db.Users.AsNoTracking().Where(u => u.Id == pharmacy.UserId)
            .Select(u => new { u.IsPendingInvitation, u.IsActive }).SingleOrDefaultAsync(ct);
        if (input.Approved == true && (account is null || account.IsPendingInvitation || !account.IsActive))
            return Conflict(new { message = "Pharmacy staff must accept the invitation before approval." });
        if (pharmacy.IsApproved != input.Approved!.Value)
        {
            pharmacy.IsApproved = input.Approved.Value;
            db.Notifications.Add(new UserNotification
            {
                RecipientUserId = pharmacy.UserId, Title = "Pharmacy approval updated",
                Message = pharmacy.IsApproved ? "Your pharmacy is approved." : "Your pharmacy approval has been withdrawn.",
                ReferenceType = "PharmacyProfile", ReferenceId = pharmacy.Id
            });
            Audit(pharmacy.IsApproved ? "pharmacy.approved" : "pharmacy.rejected", "PharmacyProfile", pharmacy.Id.ToString());
            await db.SaveChangesAsync(ct);
        }
        return Ok(new { pharmacy.Id, pharmacy.IsApproved });
    }

    [HttpGet("users")]
    public async Task<IActionResult> ListUsers([FromQuery] int page = 1, CancellationToken ct = default)
    {
        page = Math.Clamp(page, 1, 10000);
        var list = await db.Users.AsNoTracking().OrderByDescending(u => u.RegisteredAtUtc)
            .Skip((page - 1) * 50).Take(50)
            .Select(u => new { u.Id, u.Email, u.FullName, u.RegisteredAtUtc,
                u.EmailConfirmed, u.IsActive, u.IsPendingInvitation })
            .ToListAsync(ct);
        var ids = list.Select(u => u.Id).ToArray();
        var roles = await (from ur in db.UserRoles.AsNoTracking()
                           join role in db.Roles.AsNoTracking() on ur.RoleId equals role.Id
                           where ids.Contains(ur.UserId)
                           select new { ur.UserId, role.Name }).ToListAsync(ct);
        return Ok(list.Select(u => new { u.Id, u.Email, u.FullName, u.RegisteredAtUtc,
            u.EmailConfirmed, u.IsActive, u.IsPendingInvitation,
            Roles = roles.Where(r => r.UserId == u.Id).Select(r => r.Name).ToArray() }));
    }

    [HttpPatch("users/{userId:guid}/status")]
    public async Task<IActionResult> ChangeUserStatus(Guid userId, [FromBody] ChangeUserStatusRequest input,
        CancellationToken ct)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null) return NotFound();
        if (user.Id == User.CurrentUserId() || await users.IsInRoleAsync(user, AppRoles.Admin))
            return StatusCode(403, new { message = "Admin accounts cannot be disabled through this endpoint." });
        if (user.IsPendingInvitation && input.IsActive == true)
            return Conflict(new { message = "The user must accept the invitation before activation." });
        if (user.IsActive != input.IsActive!.Value)
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            user.IsActive = input.IsActive.Value;
            user.TokenVersion++;
            var updated = await users.UpdateAsync(user);
            if (!updated.Succeeded) return Problem("Unable to update user.", statusCode: 500);
            if (!user.IsActive) await tokens.RevokeAllAsync(user.Id, ct);
            Audit(user.IsActive ? "user.activated" : "user.deactivated", "User", user.Id.ToString());
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        return Ok(new { user.Id, user.IsActive });
    }

    [HttpGet("audit")]
    public async Task<IActionResult> AuditLog([FromQuery] int page = 1, CancellationToken ct = default)
    {
        page = Math.Clamp(page, 1, 10000);
        var logs = await db.AuditEvents.AsNoTracking().OrderByDescending(x => x.OccurredAtUtc)
            .ThenByDescending(x => x.Id).Skip((page - 1) * 50).Take(50)
            .Select(x => new { x.Id, x.ActorUserId, x.Action, x.EntityType, x.EntityId, x.OccurredAtUtc })
            .ToListAsync(ct);
        return Ok(logs);
    }

    private void Audit(string action, string entityType, string entityId) => db.AuditEvents.Add(new AuditEvent
    {
        ActorUserId = User.CurrentUserId(), Action = action, EntityType = entityType, EntityId = entityId
    });
}

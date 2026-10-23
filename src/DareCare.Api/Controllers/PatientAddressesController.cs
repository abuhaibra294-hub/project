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
[Route("api/v1/patients/me/addresses")]
[Authorize(Roles = AppRoles.Patient)]
public sealed class PatientAddressesController(DareCareDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> All(CancellationToken ct)
    {
        var userId = User.CurrentUserId();
        var rows = await db.PatientAddresses.AsNoTracking()
            .Where(a => a.PatientUserId == userId)
            .OrderByDescending(a => a.IsDefault).ThenBy(a => a.CreatedAtUtc)
            .Select(a => new PatientAddressResponse(a.Id, a.Label, a.City, a.FullAddress,
                a.Notes, a.IsDefault, a.CreatedAtUtc))
            .ToListAsync(ct);
        return Ok(rows);
    }

    [HttpGet("{addressId:guid}")]
    public async Task<IActionResult> One(Guid addressId, CancellationToken ct)
    {
        var userId = User.CurrentUserId();
        var item = await db.PatientAddresses.AsNoTracking()
            .Where(a => a.Id == addressId && a.PatientUserId == userId)
            .Select(a => new PatientAddressResponse(a.Id, a.Label, a.City, a.FullAddress,
                a.Notes, a.IsDefault, a.CreatedAtUtc))
            .FirstOrDefaultAsync(ct);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SavePatientAddressRequest input, CancellationToken ct)
    {
        if (!IsValid(input)) return BadRequest(new { message = "Label, city and full address cannot be blank." });
        var userId = User.CurrentUserId();
        var count = await db.PatientAddresses.CountAsync(a => a.PatientUserId == userId, ct);
        if (count >= 10) return Conflict(new { message = "Maximum 10 saved addresses per patient." });
        var makeDefault = input.IsDefault || count == 0;
        var item = new PatientAddress
        {
            PatientUserId = userId, Label = input.Label.Trim(), City = input.City.Trim(),
            FullAddress = input.FullAddress.Trim(), Notes = Normalize(input.Notes), IsDefault = makeDefault
        };
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        if (makeDefault)
            await ClearOtherDefaults(userId, ct);
        db.PatientAddresses.Add(item);
        db.AuditEvents.Add(Audit(userId, "address.created", item.Id));
        try { await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); }
        catch (DbUpdateException ex) when (DbConflict.IsUniqueViolation(ex))
        { return Conflict(new { message = "Concurrent address update. Reload and retry." }); }
        return Created($"/api/v1/patients/me/addresses/{item.Id}", ToResponse(item));
    }

    [HttpPut("{addressId:guid}")]
    public async Task<IActionResult> Update(Guid addressId,
        [FromBody] SavePatientAddressRequest input, CancellationToken ct)
    {
        if (!IsValid(input)) return BadRequest(new { message = "Label, city and full address cannot be blank." });
        var userId = User.CurrentUserId();
        var item = await db.PatientAddresses.SingleOrDefaultAsync(a => a.Id == addressId && a.PatientUserId == userId, ct);
        if (item is null) return NotFound();
        item.Label = input.Label.Trim();
        item.City = input.City.Trim();
        item.FullAddress = input.FullAddress.Trim();
        item.Notes = Normalize(input.Notes);
        item.UpdatedAtUtc = DateTime.UtcNow;
        db.AuditEvents.Add(Audit(userId, "address.updated", item.Id));
        if (input.IsDefault && !item.IsDefault)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            await ClearOtherDefaults(userId, ct);
            item.IsDefault = true;
            try { await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); }
            catch (DbUpdateException ex) when (DbConflict.IsUniqueViolation(ex))
            { return Conflict(new { message = "Concurrent default address update." }); }
        }
        else await db.SaveChangesAsync(ct);
        return Ok(ToResponse(item));
    }

    [HttpPut("{addressId:guid}/default")]
    public async Task<IActionResult> SelectDefault(Guid addressId, CancellationToken ct)
    {
        var userId = User.CurrentUserId();
        var item = await db.PatientAddresses.SingleOrDefaultAsync(a => a.Id == addressId && a.PatientUserId == userId, ct);
        if (item is null) return NotFound();
        if (item.IsDefault) return Ok(ToResponse(item));
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await ClearOtherDefaults(userId, ct);
        item.IsDefault = true;
        item.UpdatedAtUtc = DateTime.UtcNow;
        db.AuditEvents.Add(Audit(userId, "address.default.selected", item.Id));
        try { await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); }
        catch (DbUpdateException ex) when (DbConflict.IsUniqueViolation(ex))
        { return Conflict(new { message = "Concurrent default address update." }); }
        return Ok(ToResponse(item));
    }

    [HttpDelete("{addressId:guid}")]
    public async Task<IActionResult> Delete(Guid addressId, CancellationToken ct)
    {
        var userId = User.CurrentUserId();
        var item = await db.PatientAddresses.SingleOrDefaultAsync(a => a.Id == addressId && a.PatientUserId == userId, ct);
        if (item is null) return NotFound();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        db.PatientAddresses.Remove(item);
        db.AuditEvents.Add(Audit(userId, "address.deleted", addressId));
        await db.SaveChangesAsync(ct);
        if (item.IsDefault)
        {
            var replacement = await db.PatientAddresses
                .Where(a => a.PatientUserId == userId).OrderBy(a => a.CreatedAtUtc).FirstOrDefaultAsync(ct);
            if (replacement is not null)
            {
                replacement.IsDefault = true;
                replacement.UpdatedAtUtc = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
            }
        }
        await transaction.CommitAsync(ct);
        return NoContent();
    }

    private Task<int> ClearOtherDefaults(Guid userId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        return db.PatientAddresses.Where(a => a.PatientUserId == userId && a.IsDefault)
            .ExecuteUpdateAsync(setters => setters.SetProperty(a => a.IsDefault, false)
                .SetProperty(a => a.UpdatedAtUtc, now), ct);
    }

    private static bool IsValid(SavePatientAddressRequest input) =>
        !string.IsNullOrWhiteSpace(input.Label) && input.Label.Trim().Length >= 2 &&
        !string.IsNullOrWhiteSpace(input.City) && input.City.Trim().Length >= 2 &&
        !string.IsNullOrWhiteSpace(input.FullAddress) && input.FullAddress.Trim().Length >= 8;

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static PatientAddressResponse ToResponse(PatientAddress a) =>
        new(a.Id, a.Label, a.City, a.FullAddress, a.Notes, a.IsDefault, a.CreatedAtUtc);
    private static AuditEvent Audit(Guid userId, string action, Guid addressId) =>
        new() { ActorUserId = userId, Action = action, EntityType = "PatientAddress", EntityId = addressId.ToString() };
}

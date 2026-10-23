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
[Route("api/v1/orders")]
[Authorize]
public sealed class PharmacyOrdersController(DareCareDbContext db) : ControllerBase
{
    [HttpPost("prescriptions/{prescriptionId:guid}/quote")]
    [Authorize(Roles = AppRoles.Pharmacy)]
    public async Task<IActionResult> Quote(Guid prescriptionId, [FromBody] QuotePrescriptionRequest input, CancellationToken ct)
    {
        if (input.Amount <= 0 || input.Amount > 1_000_000m)
            return BadRequest(new { message = "Quote must be between 0.01 and 1,000,000." });
        var pharmacy = await ApprovedPharmacyAsync(ct);
        if (pharmacy is null) return Forbid();
        var prescription = await db.Prescriptions.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == prescriptionId && p.PharmacyProfileId == pharmacy.Id, ct);
        if (prescription is null) return NotFound();
        if (await db.PharmacyOrders.AnyAsync(o => o.PrescriptionId == prescription.Id, ct))
            return Conflict(new { message = "This prescription already has a quote." });
        var order = new PharmacyOrder
        {
            PrescriptionId = prescription.Id, PatientUserId = prescription.PatientUserId,
            PharmacyProfileId = pharmacy.Id, QuoteAmount = input.Amount,
            QuoteNotes = input.Notes?.Trim()
        };
        db.PharmacyOrders.Add(order);
        AddAudit(order, "order.quoted");
        AddNotification(order.PatientUserId, "Pharmacy quote", "Review and confirm your pharmacy quote.", order.Id);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (DbConflict.IsUniqueViolation(ex))
        {
            return Conflict(new { message = "This prescription was quoted concurrently." });
        }
        return CreatedAtAction(nameof(Get), new { id = order.Id }, Summary(order));
    }

    [HttpPost("{id:guid}/confirm")]
    [Authorize(Roles = AppRoles.Patient)]
    public Task<IActionResult> Confirm(Guid id, CancellationToken ct) =>
        Transition(id, PharmacyOrderStatus.Confirmed, byPatient: true, ct);

    [HttpPost("{id:guid}/decline")]
    [Authorize(Roles = AppRoles.Patient)]
    public Task<IActionResult> Decline(Guid id, CancellationToken ct) =>
        Transition(id, PharmacyOrderStatus.Declined, byPatient: true, ct);

    [HttpPost("{id:guid}/prepare")]
    [Authorize(Roles = AppRoles.Pharmacy)]
    public Task<IActionResult> Prepare(Guid id, CancellationToken ct) =>
        Transition(id, PharmacyOrderStatus.Preparing, byPatient: false, ct);

    [HttpPost("{id:guid}/dispatch")]
    [Authorize(Roles = AppRoles.Pharmacy)]
    public Task<IActionResult> Dispatch(Guid id, CancellationToken ct) =>
        Transition(id, PharmacyOrderStatus.OutForDelivery, byPatient: false, ct);

    [HttpPost("{id:guid}/deliver")]
    [Authorize(Roles = AppRoles.Pharmacy)]
    public Task<IActionResult> Deliver(Guid id, CancellationToken ct) =>
        Transition(id, PharmacyOrderStatus.Delivered, byPatient: false, ct);

    [HttpGet("mine")]
    [Authorize(Roles = AppRoles.Patient)]
    public async Task<IActionResult> Mine(CancellationToken ct)
    {
        var userId = User.CurrentUserId();
        return Ok(await db.PharmacyOrders.AsNoTracking().Where(o => o.PatientUserId == userId)
            .OrderByDescending(o => o.CreatedAtUtc).Take(100)
            .Select(o => new { o.Id, o.PrescriptionId, o.PharmacyProfileId, o.QuoteAmount, o.QuoteNotes,
                o.Status, o.CreatedAtUtc, o.UpdatedAtUtc }).ToListAsync(ct));
    }

    [HttpGet("inbox")]
    [Authorize(Roles = AppRoles.Pharmacy)]
    public async Task<IActionResult> Inbox(CancellationToken ct)
    {
        var pharmacy = await ApprovedPharmacyAsync(ct);
        if (pharmacy is null) return Forbid();
        return Ok(await db.PharmacyOrders.AsNoTracking().Where(o => o.PharmacyProfileId == pharmacy.Id)
            .OrderByDescending(o => o.CreatedAtUtc).Take(100)
            .Select(o => new { o.Id, o.PrescriptionId, o.PatientUserId, o.QuoteAmount, o.QuoteNotes,
                o.Status, o.CreatedAtUtc, o.UpdatedAtUtc }).ToListAsync(ct));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var order = await db.PharmacyOrders.AsNoTracking().SingleOrDefaultAsync(o => o.Id == id, ct);
        if (order is null) return NotFound();
        if (!await CanViewAsync(order, ct)) return Forbid();
        return Ok(Summary(order));
    }

    private async Task<IActionResult> Transition(Guid id, PharmacyOrderStatus next,
        bool byPatient, CancellationToken ct)
    {
        var order = await db.PharmacyOrders.SingleOrDefaultAsync(o => o.Id == id, ct);
        if (order is null) return NotFound();
        var userId = User.CurrentUserId();
        if (byPatient && order.PatientUserId != userId) return Forbid();
        if (!byPatient)
        {
            var pharmacy = await ApprovedPharmacyAsync(ct);
            if (pharmacy is null || pharmacy.Id != order.PharmacyProfileId) return Forbid();
        }
        try { order.TransitionTo(next); }
        catch (InvalidOperationException)
        {
            return Conflict(new { message = $"Order cannot move to {next} from {order.Status}." });
        }
        AddAudit(order, $"order.{next.ToString().ToLowerInvariant()}");
        var recipient = byPatient
            ? await db.Pharmacies.Where(p => p.Id == order.PharmacyProfileId).Select(p => p.UserId).SingleAsync(ct)
            : order.PatientUserId;
        AddNotification(recipient, "Order updated", $"Your pharmacy order is now {next}.", order.Id);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new { message = "Order updated in another session. Refresh and try again." });
        }
        return Ok(Summary(order));
    }

    private async Task<PharmacyProfile?> ApprovedPharmacyAsync(CancellationToken ct) =>
        await db.Pharmacies.AsNoTracking().Where(p => p.UserId == User.CurrentUserId() && p.IsApproved)
            .SingleOrDefaultAsync(ct);

    private async Task<bool> CanViewAsync(PharmacyOrder o, CancellationToken ct)
    {
        if (User.IsInRole(AppRoles.Admin)) return true;
        if (User.IsInRole(AppRoles.Patient)) return o.PatientUserId == User.CurrentUserId();
        if (!User.IsInRole(AppRoles.Pharmacy)) return false;
        var pharmacy = await ApprovedPharmacyAsync(ct);
        return pharmacy is not null && pharmacy.Id == o.PharmacyProfileId;
    }

    private void AddAudit(PharmacyOrder order, string action) => db.AuditEvents.Add(new AuditEvent
    {
        ActorUserId = User.CurrentUserId(), Action = action, EntityType = "PharmacyOrder",
        EntityId = order.Id.ToString()
    });

    private void AddNotification(Guid userId, string title, string message, Guid orderId) =>
        db.Notifications.Add(new UserNotification { RecipientUserId = userId,
            Title = title, Message = message, ReferenceType = "PharmacyOrder", ReferenceId = orderId });

    private static object Summary(PharmacyOrder x) => new
    {
        x.Id, x.PrescriptionId, x.PatientUserId, x.PharmacyProfileId,
        x.QuoteAmount, x.QuoteNotes, x.Status, x.CreatedAtUtc, x.UpdatedAtUtc
    };
}

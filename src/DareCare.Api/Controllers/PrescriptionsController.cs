using DareCare.Api.Contracts;
using DareCare.Api.Security;
using DareCare.Api.Services;
using DareCare.Domain;
using DareCare.Domain.Entities;
using DareCare.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DareCare.Api.Controllers;

[ApiController]
[Route("api/v1/prescriptions")]
[Authorize]
public sealed class PrescriptionsController(DareCareDbContext db, IPrivatePrescriptionStorage storage) : ControllerBase
{
    [HttpPost]
    [Authorize(Roles = AppRoles.Patient)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(9 * 1024 * 1024)]
    public async Task<IActionResult> Upload([FromForm] UploadPrescriptionRequest input, CancellationToken ct)
    {
        if (input.PharmacyProfileId == Guid.Empty || input.File is null)
            return BadRequest(new { message = "Pharmacy and a prescription PDF/JPG/PNG are required." });
        if (input.File.Length == 0 || input.File.Length > PrivatePrescriptionStorage.MaxBytes)
            return BadRequest(new { message = "File must be 1 byte to 8 MiB." });

        var pharmacy = await db.Pharmacies.AsNoTracking().Where(p => p.Id == input.PharmacyProfileId &&
                p.IsApproved && db.Users.Any(u => u.Id == p.UserId && u.IsActive && !u.IsPendingInvitation))
            .Select(p => new { p.Id, p.UserId }).SingleOrDefaultAsync(ct);
        if (pharmacy is null) return BadRequest(new { message = "Pharmacy is unavailable or not approved." });
        var userId = User.CurrentUserId();
        if (input.BookingId is { } bookingId && !await db.Bookings.AnyAsync(b =>
                b.Id == bookingId && b.PatientUserId == userId && b.Status == BookingStatus.Completed, ct))
            return BadRequest(new { message = "Booking must belong to you and be completed." });

        var fileName = Path.GetFileName(input.File.FileName.Replace('\\', '/'));
        var contentType = PrivatePrescriptionStorage.ContentTypeFor(fileName);
        if (contentType is null)
            return BadRequest(new { message = "Only PDF, PNG and JPEG files are allowed." });
        var key = await storage.StoreAsync(input.File, ct);
        if (key is null) return BadRequest(new { message = "Invalid file content, type or size." });
        var item = new Prescription
        {
            PatientUserId = userId,
            PharmacyProfileId = pharmacy.Id,
            BookingId = input.BookingId,
            PatientNotes = input.Notes?.Trim(),
            OriginalFileName = fileName.Length <= 180 ? fileName : fileName[..180],
            ContentType = contentType,
            StorageKey = key,
            FileSizeBytes = input.File.Length
        };
        try
        {
            db.Prescriptions.Add(item);
            db.AuditEvents.Add(new AuditEvent { ActorUserId = userId,
                Action = "prescription.uploaded", EntityType = "Prescription", EntityId = item.Id.ToString() });
            db.Notifications.Add(new UserNotification { RecipientUserId = pharmacy.UserId,
                Title = "New prescription", Message = "A new prescription needs pharmacist review.",
                ReferenceType = "Prescription", ReferenceId = item.Id });
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            storage.Delete(key); // DB insert failed, don't leave an untracked private document.
            throw;
        }
        return CreatedAtAction(nameof(Get), new { id = item.Id }, Summary(item));
    }

    [HttpGet("mine")]
    [Authorize(Roles = AppRoles.Patient)]
    public async Task<IActionResult> Mine(CancellationToken ct)
    {
        var id = User.CurrentUserId();
        var result = await db.Prescriptions.AsNoTracking().Where(p => p.PatientUserId == id)
            .OrderByDescending(p => p.CreatedAtUtc).Take(100)
            .Select(p => new { p.Id, p.PharmacyProfileId, Pharmacy = p.Pharmacy.BusinessName,
                p.BookingId, p.PatientNotes, p.OriginalFileName, p.FileSizeBytes, p.CreatedAtUtc,
                OrderId = db.PharmacyOrders.Where(o => o.PrescriptionId == p.Id).Select(o => (Guid?)o.Id).FirstOrDefault() })
            .ToListAsync(ct);
        return Ok(result);
    }

    [HttpGet("inbox")]
    [Authorize(Roles = AppRoles.Pharmacy)]
    public async Task<IActionResult> PharmacyInbox(CancellationToken ct)
    {
        var uid = User.CurrentUserId();
        var profileId = await db.Pharmacies.AsNoTracking().Where(x => x.UserId == uid && x.IsApproved)
            .Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
        if (profileId is null) return Forbid();
        var items = await db.Prescriptions.AsNoTracking().Where(x => x.PharmacyProfileId == profileId.Value)
            .OrderByDescending(x => x.CreatedAtUtc).Take(100)
            .Select(x => new { x.Id, x.PatientUserId, x.PatientNotes, x.OriginalFileName, x.CreatedAtUtc,
                OrderId = db.PharmacyOrders.Where(o => o.PrescriptionId == x.Id).Select(o => (Guid?)o.Id).FirstOrDefault() })
            .ToListAsync(ct);
        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var item = await db.Prescriptions.AsNoTracking().Include(p => p.Pharmacy)
            .SingleOrDefaultAsync(p => p.Id == id, ct);
        if (item is null) return NotFound();
        if (!CanView(item)) return Forbid();
        return Ok(Summary(item));
    }

    [HttpGet("{id:guid}/file")]
    public async Task<IActionResult> Download(Guid id, CancellationToken ct)
    {
        var item = await db.Prescriptions.AsNoTracking().Include(p => p.Pharmacy)
            .SingleOrDefaultAsync(p => p.Id == id, ct);
        if (item is null) return NotFound();
        if (!CanView(item)) return Forbid();
        var file = await storage.OpenReadAsync(item.StorageKey, ct);
        if (file is null) return NotFound(new { message = "Stored document not found." });
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        // Always download rather than rendering untrusted content inline.
        return File(file, item.ContentType, item.OriginalFileName);
    }

    private bool CanView(Prescription item) => User.IsInRole(AppRoles.Admin) ||
        (User.IsInRole(AppRoles.Patient) && item.PatientUserId == User.CurrentUserId()) ||
        (User.IsInRole(AppRoles.Pharmacy) && item.Pharmacy.UserId == User.CurrentUserId() && item.Pharmacy.IsApproved);

    private static object Summary(Prescription p) => new
    {
        p.Id, p.PatientUserId, p.PharmacyProfileId, p.BookingId,
        p.PatientNotes, p.OriginalFileName, p.ContentType, p.FileSizeBytes, p.CreatedAtUtc
    };
}

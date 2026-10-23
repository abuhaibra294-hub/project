using DareCare.Api.Security;
using DareCare.Application.Contracts;
using DareCare.Domain;
using DareCare.Domain.Entities;
using DareCare.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace DareCare.Api.Controllers;

[ApiController]
[Route("api/v1/bookings")]
[Authorize]
public sealed class BookingsController(DareCareDbContext db) : ControllerBase
{
    private static IQueryable<BookingResponse> Project(IQueryable<Booking> query) =>
        query.Select(x => new BookingResponse(
            x.Id, x.PatientUserId, x.NurseProfileId, x.AvailabilitySlotId,
            x.Service.Name, x.Slot.StartsAtUtc, x.Slot.EndsAtUtc,
            x.ServiceAddress, x.PriceAtBooking, x.Status));

    [HttpGet("mine")]
    [Authorize(Roles = AppRoles.Patient + "," + AppRoles.Nurse)]
    public async Task<IActionResult> Mine(CancellationToken ct)
    {
        var id = User.CurrentUserId();
        var bookings = db.Bookings.AsNoTracking().AsQueryable();
        if (User.IsInRole(AppRoles.Patient))
            bookings = bookings.Where(x => x.PatientUserId == id);
        else
            bookings = bookings.Where(x => x.Nurse.UserId == id);
        return Ok(await Project(bookings.OrderByDescending(x => x.CreatedAtUtc))
            .Take(100).ToListAsync(ct));
    }

    // Paginated booking search for the patient and nurse dashboards.
    // Existing GET /mine still returns the original array for older clients.
    [HttpGet("mine/search")]
    [Authorize(Roles = AppRoles.Patient + "," + AppRoles.Nurse)]
    public async Task<IActionResult> SearchMine([FromQuery] BookingStatus? status,
        [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        if (page is < 1 or > 100000 || pageSize is < 1 or > 50 ||
            (status.HasValue && !Enum.IsDefined(status.Value)) ||
            (from.HasValue && to.HasValue && from.Value > to.Value))
            return BadRequest(new { message = "Invalid booking search filters or pagination." });
        var userId = User.CurrentUserId();
        var query = db.Bookings.AsNoTracking().AsQueryable();
        query = User.IsInRole(AppRoles.Patient)
            ? query.Where(x => x.PatientUserId == userId)
            : query.Where(x => x.Nurse.UserId == userId);
        if (status.HasValue) query = query.Where(x => x.Status == status.Value);
        if (from.HasValue) query = query.Where(x => x.Slot.StartsAtUtc >= from.Value.UtcDateTime);
        if (to.HasValue) query = query.Where(x => x.Slot.StartsAtUtc <= to.Value.UtcDateTime);
        return Ok(new
        {
            page, pageSize, total = await query.CountAsync(ct),
            items = await Project(query.OrderByDescending(x => x.CreatedAtUtc)
                .ThenBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize)).ToListAsync(ct)
        });
    }

    [HttpGet("{id:guid}/timeline")]
    [Authorize(Roles = AppRoles.Patient + "," + AppRoles.Nurse + "," + AppRoles.Admin)]
    public async Task<IActionResult> Timeline(Guid id, CancellationToken ct)
    {
        var booking = await db.Bookings.AsNoTracking().Where(b => b.Id == id)
            .Select(b => new { b.PatientUserId, NurseUserId = b.Nurse.UserId })
            .SingleOrDefaultAsync(ct);
        if (booking is null) return NotFound();
        var uid = User.CurrentUserId();
        if (!User.IsInRole(AppRoles.Admin) && booking.PatientUserId != uid && booking.NurseUserId != uid)
            return Forbid();
        var key = id.ToString();
        return Ok(await db.AuditEvents.AsNoTracking()
            .Where(a => a.EntityType == "Booking" && a.EntityId == key)
            .OrderBy(a => a.OccurredAtUtc).ThenBy(a => a.Id)
            .Select(a => new { a.Action, a.OccurredAtUtc })
            .ToListAsync(ct));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Roles = AppRoles.Patient + "," + AppRoles.Nurse + "," + AppRoles.Admin)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var booking = await db.Bookings.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new { x.PatientUserId, NurseUserId = x.Nurse.UserId })
            .FirstOrDefaultAsync(ct);
        if (booking is null) return NotFound();
        var uid = User.CurrentUserId();
        if (!User.IsInRole(AppRoles.Admin) && booking.PatientUserId != uid && booking.NurseUserId != uid)
            return Forbid(); // Prevents cross-patient BOLA/IDOR attacks.
        var result = await Project(db.Bookings.AsNoTracking().Where(x => x.Id == id))
            .FirstAsync(ct);
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = AppRoles.Patient)]
    public async Task<IActionResult> Create([FromBody] CreateBookingRequest input, CancellationToken ct)
    {
        if (input.CareServiceId == Guid.Empty || input.AvailabilitySlotId == Guid.Empty ||
            input.NurseProfileId == Guid.Empty)
            return BadRequest(new { message = "Service, nurse and slot ids are required." });
        var patientId = User.CurrentUserId();
        string serviceAddress;
        if (input.AddressId.HasValue)
        {
            if (input.AddressId.Value == Guid.Empty || !string.IsNullOrWhiteSpace(input.ServiceAddress))
                return BadRequest(new { message = "Provide either addressId or serviceAddress." });
            var saved = await db.PatientAddresses.AsNoTracking().Where(a =>
                    a.Id == input.AddressId.Value && a.PatientUserId == patientId)
                .Select(a => new { a.City, a.FullAddress }).SingleOrDefaultAsync(ct);
            if (saved is null) return BadRequest(new { message = "Saved address is unavailable." });
            serviceAddress = $"{saved.City} - {saved.FullAddress}";
        }
        else
        {
            serviceAddress = input.ServiceAddress?.Trim() ?? "";
            if (serviceAddress.Length is < 8 or > 400)
                return BadRequest(new { message = "A service address of 8-400 characters is required." });
        }
        var service = await db.CareServices.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == input.CareServiceId && x.IsActive, ct);
        if (service is null) return BadRequest(new { message = "Service does not exist." });
        var nurse = await db.Nurses.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == input.NurseProfileId && x.IsApproved &&
                db.Users.Any(u => u.Id == x.UserId && u.IsActive && !u.IsPendingInvitation), ct);
        if (nurse is null) return BadRequest(new { message = "Nurse is not approved or does not exist." });
        var qualified = await db.NurseSkills.AnyAsync(x =>
            x.NurseProfileId == nurse.Id && x.SkillId == service.RequiredSkillId, ct);
        if (!qualified) return BadRequest(new { message = "Nurse does not have the required service skill." });
        var slot = await db.AvailabilitySlots.AsNoTracking().SingleOrDefaultAsync(x =>
            x.Id == input.AvailabilitySlotId && x.NurseProfileId == nurse.Id, ct);
        if (slot is null || slot.StartsAtUtc <= DateTime.UtcNow ||
            slot.EndsAtUtc - slot.StartsAtUtc < TimeSpan.FromMinutes(service.DurationMinutes))
            return BadRequest(new { message = "Invalid time slot for this service." });
        var alreadyBooked = await db.Bookings.AnyAsync(x => x.AvailabilitySlotId == slot.Id &&
            x.Status != BookingStatus.Cancelled && x.Status != BookingStatus.Rejected, ct);
        if (alreadyBooked) return Conflict(new { message = "Slot is already booked." });

        var booking = new Booking
        {
            PatientUserId = patientId,
            NurseProfileId = nurse.Id,
            AvailabilitySlotId = slot.Id,
            CareServiceId = service.Id,
            ServiceAddress = serviceAddress,
            PriceAtBooking = service.BasePrice
        };
        db.Bookings.Add(booking);
        db.Notifications.Add(new UserNotification
        {
            RecipientUserId = nurse.UserId, Title = "New nursing booking",
            Message = "A patient requested a home-care visit. Please review the booking.",
            ReferenceType = "Booking", ReferenceId = booking.Id
        });
        db.AuditEvents.Add(new AuditEvent
        {
            ActorUserId = patientId, Action = "booking.created",
            EntityType = "Booking", EntityId = booking.Id.ToString()
        });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (DbConflict.IsUniqueViolation(ex))
        {
            return Conflict(new { message = "Slot was just booked by another patient." });
        }
        var response = new BookingResponse(booking.Id, patientId, nurse.Id, slot.Id,
            service.Name, slot.StartsAtUtc, slot.EndsAtUtc, booking.ServiceAddress,
            booking.PriceAtBooking, booking.Status);
        return CreatedAtAction(nameof(Get), new { id = booking.Id }, response);
    }

    [HttpPost("{id:guid}/accept")]
    [Authorize(Roles = AppRoles.Nurse)]
    public Task<IActionResult> Accept(Guid id, CancellationToken ct) => Transition(id, BookingStatus.Accepted, false, ct);

    [HttpPost("{id:guid}/reject")]
    [Authorize(Roles = AppRoles.Nurse)]
    public Task<IActionResult> Reject(Guid id, CancellationToken ct) => Transition(id, BookingStatus.Rejected, false, ct);

    [HttpPost("{id:guid}/start")]
    [Authorize(Roles = AppRoles.Nurse)]
    public Task<IActionResult> Start(Guid id, CancellationToken ct) => Transition(id, BookingStatus.InProgress, false, ct);

    [HttpPost("{id:guid}/complete")]
    [Authorize(Roles = AppRoles.Nurse)]
    public Task<IActionResult> Complete(Guid id, CancellationToken ct) => Transition(id, BookingStatus.Completed, false, ct);

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Roles = AppRoles.Patient)]
    public Task<IActionResult> Cancel(Guid id, CancellationToken ct) => Transition(id, BookingStatus.Cancelled, true, ct);

    private async Task<IActionResult> Transition(Guid id, BookingStatus next, bool byPatient, CancellationToken ct)
    {
        var booking = await db.Bookings.Include(x => x.Nurse)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (booking is null) return NotFound();
        var currentId = User.CurrentUserId();
        if ((byPatient && booking.PatientUserId != currentId) ||
            (!byPatient && booking.Nurse.UserId != currentId))
            return Forbid();
        try
        {
            booking.TransitionTo(next);
        }
        catch (InvalidOperationException)
        {
            return Conflict(new { message = $"Booking cannot change from {booking.Status} to {next}." });
        }
        db.Notifications.Add(new UserNotification
        {
            RecipientUserId = byPatient ? booking.Nurse.UserId : booking.PatientUserId,
            Title = "Booking status updated",
            Message = $"Your home-care booking status changed to {next}.",
            ReferenceType = "Booking", ReferenceId = booking.Id
        });
        db.AuditEvents.Add(new AuditEvent
        {
            ActorUserId = currentId, Action = $"booking.{next.ToString().ToLowerInvariant()}",
            EntityType = "Booking", EntityId = booking.Id.ToString()
        });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new { message = "Booking changed; reload before trying again." });
        }
        return Ok(new { booking.Id, booking.Status });
    }
}

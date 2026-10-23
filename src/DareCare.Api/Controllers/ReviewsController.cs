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
[Route("api/v1/reviews")]
public sealed class ReviewsController(DareCareDbContext db) : ControllerBase
{
    [HttpPost("bookings/{bookingId:guid}")]
    [Authorize(Roles = AppRoles.Patient)]
    public async Task<IActionResult> Create(Guid bookingId, [FromBody] CreateNurseReviewRequest input,
        CancellationToken ct)
    {
        var patientId = User.CurrentUserId();
        var booking = await db.Bookings.AsNoTracking()
            .Where(b => b.Id == bookingId)
            .Select(b => new { b.Id, b.PatientUserId, b.NurseProfileId, b.Status })
            .SingleOrDefaultAsync(ct);
        if (booking is null) return NotFound();
        // Do not disclose booking status, nurse, or patient to unrelated accounts.
        if (booking.PatientUserId != patientId) return Forbid();
        if (booking.Status != BookingStatus.Completed)
            return Conflict(new { message = "A visit may be reviewed only after completion." });
        if (await db.NurseReviews.AnyAsync(r => r.BookingId == bookingId, ct))
            return Conflict(new { message = "This visit has already been reviewed." });

        var review = NurseReview.Create(booking.Id, patientId, booking.NurseProfileId,
            input.Rating, input.Comment);
        db.NurseReviews.Add(review);
        db.AuditEvents.Add(new AuditEvent
        {
            ActorUserId = patientId, Action = "review.created", EntityType = "NurseReview",
            EntityId = review.Id.ToString()
        });
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (DbConflict.IsUniqueViolation(ex))
        {
            // Unique BookingId constraint handles simultaneous submissions.
            return Conflict(new { message = "This visit was reviewed already." });
        }
        return CreatedAtAction(nameof(Get), new { id = review.Id }, new { review.Id, review.BookingId,
            review.Rating, review.Comment, review.CreatedAtUtc });
    }

    [HttpGet("{id:guid}")]
    [Authorize(Roles = AppRoles.Patient)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var review = await db.NurseReviews.AsNoTracking().Where(r => r.Id == id)
            .Select(r => new { r.Id, r.PatientUserId, r.BookingId, r.NurseProfileId,
                r.Rating, r.Comment, r.CreatedAtUtc }).SingleOrDefaultAsync(ct);
        if (review is null) return NotFound();
        if (review.PatientUserId != User.CurrentUserId()) return Forbid();
        return Ok(new { review.Id, review.BookingId, review.NurseProfileId,
            review.Rating, review.Comment, review.CreatedAtUtc });
    }

    [HttpGet("mine")]
    [Authorize(Roles = AppRoles.Patient)]
    public async Task<IActionResult> GetMine(CancellationToken ct)
    {
        var id = User.CurrentUserId();
        return Ok(await db.NurseReviews.AsNoTracking()
            .Where(r => r.PatientUserId == id)
            .OrderByDescending(r => r.CreatedAtUtc).Take(100)
            .Select(r => new { r.Id, r.BookingId, r.NurseProfileId,
                r.Rating, r.Comment, r.CreatedAtUtc }).ToListAsync(ct));
    }

    [HttpGet("nurses/{nurseProfileId:guid}/summary")]
    [AllowAnonymous]
    public async Task<IActionResult> NurseSummary(Guid nurseProfileId, CancellationToken ct)
    {
        var exists = await db.Nurses.AsNoTracking().AnyAsync(n => n.Id == nurseProfileId &&
            n.IsApproved && db.Users.Any(u => u.Id == n.UserId && u.IsActive && !u.IsPendingInvitation), ct);
        if (!exists) return NotFound();
        var summary = await db.NurseReviews.AsNoTracking()
            .Where(r => r.NurseProfileId == nurseProfileId)
            .GroupBy(r => r.NurseProfileId)
            .Select(g => new { count = g.Count(), average = g.Average(r => (double)r.Rating) })
            .SingleOrDefaultAsync(ct);
        return Ok(new { nurseProfileId,
            count = summary?.count ?? 0,
            averageRating = summary is null ? (double?)null : Math.Round(summary.average, 1),
            note = "Aggregated ratings only; review text and patient identity are private." });
    }
}

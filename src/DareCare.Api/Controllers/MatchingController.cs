using DareCare.Domain;
using DareCare.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DareCare.Api.Controllers;

[ApiController]
[Route("api/v1/matching")]
public sealed class MatchingController(DareCareDbContext db) : ControllerBase
{
    // Explainable rule-based suggestions, not a machine-learning medical diagnosis.
    // A real location/distance integration and evaluation set will be added later.
    [HttpGet("nurses")]
    public async Task<IActionResult> Recommend(
        [FromQuery] Guid serviceId,
        [FromQuery] DateTimeOffset? startsAt,
        [FromQuery] string? city,
        CancellationToken ct)
    {
        if (serviceId == Guid.Empty) return BadRequest(new { message = "serviceId is required." });
        var service = await db.CareServices.AsNoTracking()
            .Where(x => x.Id == serviceId && x.IsActive)
            .Select(x => new { x.Id, x.Name, x.RequiredSkillId, x.DurationMinutes })
            .SingleOrDefaultAsync(ct);
        if (service is null) return NotFound(new { message = "Service not found." });
        if (city?.Length > 120) return BadRequest(new { message = "City too long." });
        var now = DateTime.UtcNow;
        var requested = startsAt?.UtcDateTime;
        if (requested is not null && (requested < now.AddMinutes(10) || requested > now.AddDays(90)))
            return BadRequest(new { message = "Requested time must be 10 minutes to 90 days ahead." });

        var eligible = await db.Nurses.AsNoTracking()
            .Where(n => n.IsApproved && n.NurseSkills.Any(s => s.SkillId == service.RequiredSkillId))
            .Where(n => db.Users.Any(u => u.Id == n.UserId && u.IsActive && !u.IsPendingInvitation))
            .OrderByDescending(n => n.YearsOfExperience).Take(250)
            .Select(n => new { n.Id, n.UserId, n.City, n.ServiceArea,
                n.YearsOfExperience, n.HourlyRate,
                FullName = db.Users.Where(u => u.Id == n.UserId).Select(u => u.FullName).FirstOrDefault() })
            .ToListAsync(ct);
        if (eligible.Count == 0) return Ok(new { serviceId, method = "rules-v2", recommendations = Array.Empty<object>() });
        var nurseIds = eligible.Select(n => n.Id).ToList();
        var slots = await db.AvailabilitySlots.AsNoTracking()
            .Where(s => nurseIds.Contains(s.NurseProfileId) && s.StartsAtUtc > now.AddMinutes(10))
            .Where(s => requested == null || s.StartsAtUtc == requested)
            .Where(s => !db.Bookings.Any(b => b.AvailabilitySlotId == s.Id &&
                b.Status != BookingStatus.Cancelled && b.Status != BookingStatus.Rejected))
            .OrderBy(s => s.StartsAtUtc).Take(1000)
            .Select(s => new { s.Id, s.NurseProfileId, s.StartsAtUtc, s.EndsAtUtc })
            .ToListAsync(ct);

        // Only aggregates are queried. Private patient comments are never read or exposed.
        var ratings = await db.NurseReviews.AsNoTracking()
            .Where(r => nurseIds.Contains(r.NurseProfileId))
            .GroupBy(r => r.NurseProfileId)
            .Select(g => new { NurseProfileId = g.Key,
                Count = g.Count(), Average = g.Average(r => (double)r.Rating) })
            .ToListAsync(ct);
        var ratingByNurse = ratings.ToDictionary(r => r.NurseProfileId);
        var place = city?.Trim();
        var recommendations = eligible.Select(n =>
        {
            var firstSlot = slots.FirstOrDefault(s => s.NurseProfileId == n.Id &&
                s.EndsAtUtc - s.StartsAtUtc >= TimeSpan.FromMinutes(service.DurationMinutes));
            if (firstSlot is null) return null;
            var cityMatch = !string.IsNullOrWhiteSpace(place) &&
                (string.Equals(n.City, place, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(n.ServiceArea, place, StringComparison.OrdinalIgnoreCase));
            var rated = ratingByNurse.TryGetValue(n.Id, out var stats);
            var averageRating = rated ? Math.Round(stats!.Average, 1) : (double?)null;
            // Explainable 0..100 score; not an ML model or clinical safety assessment.
            var score = 50 + 15 + (cityMatch ? 15 : 0) + Math.Min(n.YearsOfExperience, 10)
                + (rated ? (int)Math.Round(stats!.Average * 2) : 0);
            var reasons = new List<string> { "Verified approved nurse", "Required skill", "Free time slot" };
            if (cityMatch) reasons.Add("City or service-area matches");
            reasons.Add($"{n.YearsOfExperience} years of experience");
            if (rated) reasons.Add($"Average patient rating {averageRating}/5 ({stats!.Count} verified visits)");
            return new
            {
                n.Id, n.FullName, n.City, n.ServiceArea, n.YearsOfExperience, n.HourlyRate,
                slotId = firstSlot.Id, firstSlot.StartsAtUtc, firstSlot.EndsAtUtc,
                averageRating, reviewCount = rated ? stats!.Count : 0,
                score, reasons
            };
        }).Where(r => r is not null)
            .OrderByDescending(r => r!.score)
            .ThenBy(r => r!.StartsAtUtc)
            .Take(10).ToList();
        return Ok(new
        {
            serviceId, service = service.Name, method = "rules-v2",
            disclaimer = "This ranks available qualified nurses; it does not provide a medical diagnosis or verified travel distance.",
            recommendations
        });
    }
}

using DareCare.Api.Security;
using DareCare.Domain;
using DareCare.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DareCare.Api.Controllers;

// Small, role-scoped API for Flutter and the web portals. No cross-user details.
[ApiController]
[Route("api/v1/dashboard")]
[Authorize]
public sealed class DashboardController(DareCareDbContext db) : ControllerBase
{
    [HttpGet("me")]
    public async Task<IActionResult> Mine(CancellationToken ct)
    {
        var uid = User.CurrentUserId();
        var unread = await db.Notifications.CountAsync(n => n.RecipientUserId == uid && n.ReadAtUtc == null, ct);
        var myOpenSupportTickets = await db.SupportTickets.CountAsync(t => t.OwnerUserId == uid &&
            t.Status != SupportTicketStatus.Closed && t.Status != SupportTicketStatus.Resolved, ct);
        if (User.IsInRole(AppRoles.Patient))
        {
            return Ok(new
            {
                role = AppRoles.Patient, unreadNotifications = unread, openSupportTickets = myOpenSupportTickets,
                pendingBookings = await db.Bookings.CountAsync(b => b.PatientUserId == uid && b.Status == BookingStatus.Pending, ct),
                upcomingVisits = await db.Bookings.CountAsync(b => b.PatientUserId == uid && b.Status == BookingStatus.Accepted, ct),
                completedVisits = await db.Bookings.CountAsync(b => b.PatientUserId == uid && b.Status == BookingStatus.Completed, ct),
                completedPharmacyOrders = await db.PharmacyOrders.CountAsync(o => o.PatientUserId == uid && o.Status == PharmacyOrderStatus.Delivered, ct),
                myReviews = await db.NurseReviews.CountAsync(r => r.PatientUserId == uid, ct),
                recentVisits = await db.Bookings.AsNoTracking()
                    .Where(b => b.PatientUserId == uid)
                    .OrderByDescending(b => b.CreatedAtUtc).Take(5)
                    .Select(b => new { b.Id, b.Status, b.Slot.StartsAtUtc, service = b.Service.Name })
                    .ToListAsync(ct)
            });
        }
        if (User.IsInRole(AppRoles.Nurse))
        {
            var nurse = await db.Nurses.AsNoTracking().Where(n => n.UserId == uid)
                .Select(n => new { n.Id, n.IsApproved }).SingleOrDefaultAsync(ct);
            if (nurse is null) return NotFound();
            return Ok(new
            {
                role = AppRoles.Nurse, unreadNotifications = unread, openSupportTickets = myOpenSupportTickets, nurse.IsApproved,
                pendingRequests = await db.Bookings.CountAsync(b => b.NurseProfileId == nurse.Id && b.Status == BookingStatus.Pending, ct),
                acceptedVisits = await db.Bookings.CountAsync(b => b.NurseProfileId == nurse.Id && b.Status == BookingStatus.Accepted, ct),
                completedVisits = await db.Bookings.CountAsync(b => b.NurseProfileId == nurse.Id && b.Status == BookingStatus.Completed, ct),
                upcomingSlots = await db.AvailabilitySlots.CountAsync(s => s.NurseProfileId == nurse.Id && s.StartsAtUtc > DateTime.UtcNow, ct),
                ratingCount = await db.NurseReviews.CountAsync(r => r.NurseProfileId == nurse.Id, ct)
            });
        }
        if (User.IsInRole(AppRoles.Pharmacy))
        {
            var pharmacy = await db.Pharmacies.AsNoTracking().Where(p => p.UserId == uid)
                .Select(p => new { p.Id, p.IsApproved }).SingleOrDefaultAsync(ct);
            if (pharmacy is null) return NotFound();
            return Ok(new
            {
                role = AppRoles.Pharmacy, unreadNotifications = unread, openSupportTickets = myOpenSupportTickets, pharmacy.IsApproved,
                prescriptionsReceived = await db.Prescriptions.CountAsync(p => p.PharmacyProfileId == pharmacy.Id, ct),
                pendingQuotes = await db.Prescriptions.CountAsync(p => p.PharmacyProfileId == pharmacy.Id &&
                    !db.PharmacyOrders.Any(o => o.PrescriptionId == p.Id), ct),
                ordersAwaitingConfirmation = await db.PharmacyOrders.CountAsync(o => o.PharmacyProfileId == pharmacy.Id && o.Status == PharmacyOrderStatus.Quoted, ct),
                ordersInProgress = await db.PharmacyOrders.CountAsync(o => o.PharmacyProfileId == pharmacy.Id &&
                    (o.Status == PharmacyOrderStatus.Confirmed || o.Status == PharmacyOrderStatus.Preparing || o.Status == PharmacyOrderStatus.OutForDelivery), ct),
                deliveredOrders = await db.PharmacyOrders.CountAsync(o => o.PharmacyProfileId == pharmacy.Id && o.Status == PharmacyOrderStatus.Delivered, ct)
            });
        }
        if (User.IsInRole(AppRoles.Admin))
        {
            return Ok(new
            {
                role = AppRoles.Admin, unreadNotifications = unread, openSupportTickets = myOpenSupportTickets,
                totalPatients = await db.Patients.CountAsync(ct),
                pendingNurses = await db.Nurses.CountAsync(n => !n.IsApproved, ct),
                pendingPharmacies = await db.Pharmacies.CountAsync(p => !p.IsApproved, ct),
                totalBookings = await db.Bookings.CountAsync(ct),
                totalPrescriptionOrders = await db.PharmacyOrders.CountAsync(ct),
                completedReviews = await db.NurseReviews.CountAsync(ct),
                pendingSupportTickets = await db.SupportTickets.CountAsync(t =>
                    t.Status == SupportTicketStatus.Open || t.Status == SupportTicketStatus.InProgress, ct)
            });
        }
        return Forbid();
    }
}

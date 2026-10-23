using DareCare.Api.Security;
using DareCare.Application.Contracts;
using DareCare.Domain;
using DareCare.Domain.Entities;
using DareCare.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace DareCare.Api.Controllers;

// Ticket bodies can contain sensitive personal information: only the owner and admins may read them.
[ApiController]
[Route("api/v1/support")]
[Authorize]
public sealed class SupportController(DareCareDbContext db) : ControllerBase
{
    [HttpPost("tickets")]
    [EnableRateLimiting("support-write")]
    public async Task<IActionResult> Create([FromBody] CreateSupportTicketRequest input, CancellationToken ct)
    {
        if (!Enum.IsDefined(input.Category)) return BadRequest(new { message = "Invalid ticket category." });
        if (string.IsNullOrWhiteSpace(input.Subject) || input.Subject.Trim().Length < 5 ||
            string.IsNullOrWhiteSpace(input.Message) || input.Message.Trim().Length < 10)
            return BadRequest(new { message = "Please provide a subject and a description." });
        var userId = User.CurrentUserId();
        var ticket = SupportTicket.Create(userId, input.Category, input.Subject);
        db.SupportTickets.Add(ticket);
        db.SupportMessages.Add(SupportMessage.Create(ticket.Id, userId, input.Message, false));
        db.AuditEvents.Add(Audit(userId, "support.created", ticket.Id));
        await NotifyAdmins(ticket.Id, userId, ct);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { id = ticket.Id },
            new { ticket.Id, ticket.Subject, ticket.Category, ticket.Status, ticket.CreatedAtUtc });
    }

    [HttpGet("tickets/mine")]
    public async Task<IActionResult> Mine([FromQuery] SupportTicketStatus? status,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        if (!ValidPage(page, pageSize) || (status is not null && !Enum.IsDefined(status.Value)))
            return BadRequest(new { message = "Invalid filters or page size (1-50)." });
        var userId = User.CurrentUserId();
        var query = db.SupportTickets.AsNoTracking().Where(t => t.OwnerUserId == userId);
        if (status.HasValue) query = query.Where(t => t.Status == status.Value);
        return Ok(new { page, pageSize, total = await query.CountAsync(ct),
            items = await Summaries(query.OrderByDescending(t => t.CreatedAtUtc)
                .ThenBy(t => t.Id).Skip((page - 1) * pageSize).Take(pageSize)).ToListAsync(ct) });
    }

    [HttpGet("tickets/{id:guid}")]
    public async Task<IActionResult> Get(Guid id, [FromQuery] int messagePage = 1,
        [FromQuery] int messagePageSize = 50, CancellationToken ct = default)
    {
        if (!ValidPage(messagePage, messagePageSize)) return BadRequest(new { message = "Invalid message pagination." });
        var ticket = await db.SupportTickets.AsNoTracking().SingleOrDefaultAsync(t => t.Id == id, ct);
        if (ticket is null) return NotFound();
        if (!IsOwnerOrAdmin(ticket)) return Forbid();
        var messages = db.SupportMessages.AsNoTracking().Where(m => m.SupportTicketId == ticket.Id);
        return Ok(new
        {
            ticket.Id, ticket.Subject, ticket.Category, ticket.Status,
            ticket.CreatedAtUtc, ticket.UpdatedAtUtc, messagePage, messagePageSize,
            totalMessages = await messages.CountAsync(ct),
            messages = await messages.OrderBy(m => m.CreatedAtUtc).ThenBy(m => m.Id)
                .Skip((messagePage - 1) * messagePageSize).Take(messagePageSize)
                .Select(m => new { m.Id, m.SenderUserId, m.IsStaffReply, m.Body, m.CreatedAtUtc })
                .ToListAsync(ct)
        });
    }

    [HttpPost("tickets/{id:guid}/messages")]
    [EnableRateLimiting("support-write")]
    public async Task<IActionResult> Reply(Guid id, [FromBody] ReplySupportTicketRequest input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Message))
            return BadRequest(new { message = "Message must not be blank." });
        var ticket = await db.SupportTickets.SingleOrDefaultAsync(t => t.Id == id, ct);
        if (ticket is null) return NotFound();
        if (!IsOwnerOrAdmin(ticket)) return Forbid();
        if (ticket.Status == SupportTicketStatus.Closed)
            return Conflict(new { message = "Closed tickets cannot accept messages. Open a new ticket." });
        var isAdmin = User.IsInRole(AppRoles.Admin);
        var sender = User.CurrentUserId();
        if (ticket.Status == SupportTicketStatus.Resolved)
            ticket.ChangeStatus(SupportTicketStatus.InProgress);
        else ticket.Touch();
        var message = SupportMessage.Create(id, sender, input.Message, isAdmin);
        db.SupportMessages.Add(message);
        db.AuditEvents.Add(Audit(sender, "support.replied", id));
        if (isAdmin)
            Notify(ticket.OwnerUserId, "Support team replied", "You have a new support reply.", id);
        else await NotifyAdmins(id, sender, ct);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException)
        { return Conflict(new { message = "Ticket changed. Refresh it and try again." }); }
        return Ok(new { message.Id, message.CreatedAtUtc, ticket.Status });
    }

    [HttpGet("admin/tickets")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> AdminTickets([FromQuery] SupportTicketStatus? status,
        [FromQuery] SupportTicketCategory? category, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        if (!ValidPage(page, pageSize) ||
            (status.HasValue && !Enum.IsDefined(status.Value)) ||
            (category.HasValue && !Enum.IsDefined(category.Value)))
            return BadRequest(new { message = "Invalid filter or page size." });
        var query = db.SupportTickets.AsNoTracking().AsQueryable();
        if (status.HasValue) query = query.Where(t => t.Status == status.Value);
        if (category.HasValue) query = query.Where(t => t.Category == category.Value);
        return Ok(new { page, pageSize, total = await query.CountAsync(ct),
            items = await Summaries(query.OrderByDescending(t => t.CreatedAtUtc)
                .ThenBy(t => t.Id).Skip((page - 1) * pageSize).Take(pageSize)).ToListAsync(ct) });
    }

    [HttpPatch("admin/tickets/{id:guid}/status")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> SetStatus(Guid id, [FromBody] UpdateSupportStatusRequest input,
        CancellationToken ct)
    {
        if (input.Status is null || !Enum.IsDefined(input.Status.Value))
            return BadRequest(new { message = "A valid status is required." });
        var ticket = await db.SupportTickets.SingleOrDefaultAsync(t => t.Id == id, ct);
        if (ticket is null) return NotFound();
        if (ticket.Status == input.Status.Value) return Ok(new { ticket.Id, ticket.Status });
        try { ticket.ChangeStatus(input.Status.Value); }
        catch (InvalidOperationException)
        { return Conflict(new { message = "This ticket status transition is not allowed." }); }
        db.AuditEvents.Add(Audit(User.CurrentUserId(), $"support.{ticket.Status.ToString().ToLowerInvariant()}", id));
        Notify(ticket.OwnerUserId, "Support ticket updated", $"Ticket status is now {ticket.Status}.", id);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException)
        { return Conflict(new { message = "Ticket changed. Reload it before editing." }); }
        return Ok(new { ticket.Id, ticket.Status, ticket.UpdatedAtUtc });
    }

    private bool IsOwnerOrAdmin(SupportTicket ticket) =>
        User.IsInRole(AppRoles.Admin) || ticket.OwnerUserId == User.CurrentUserId();

    private static bool ValidPage(int page, int size) => page >= 1 && page <= 100000 && size is >= 1 and <= 50;

    private sealed record TicketSummary(Guid Id, Guid OwnerUserId, string Subject,
        SupportTicketCategory Category, SupportTicketStatus Status,
        DateTime CreatedAtUtc, DateTime UpdatedAtUtc);

    private static IQueryable<TicketSummary> Summaries(IQueryable<SupportTicket> tickets) =>
        tickets.Select(t => new TicketSummary(t.Id, t.OwnerUserId, t.Subject, t.Category,
            t.Status, t.CreatedAtUtc, t.UpdatedAtUtc));

    private async Task NotifyAdmins(Guid ticketId, Guid excludeUser, CancellationToken ct)
    {
        var adminIds = await (from userRole in db.UserRoles
                join role in db.Roles on userRole.RoleId equals role.Id
                join user in db.Users on userRole.UserId equals user.Id
                where role.Name == AppRoles.Admin && user.IsActive && user.Id != excludeUser
                select user.Id).ToListAsync(ct);
        foreach (var adminId in adminIds)
            Notify(adminId, "Support request received", "A support ticket needs attention.", ticketId);
    }

    private void Notify(Guid recipientId, string title, string message, Guid ticketId) =>
        db.Notifications.Add(new UserNotification
        {
            RecipientUserId = recipientId, Title = title, Message = message,
            ReferenceType = "SupportTicket", ReferenceId = ticketId
        });

    private static AuditEvent Audit(Guid actorId, string action, Guid ticketId) => new()
    {
        ActorUserId = actorId, Action = action, EntityType = "SupportTicket", EntityId = ticketId.ToString()
    };
}

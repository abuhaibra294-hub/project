using DareCare.Api.Security;
using DareCare.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DareCare.Api.Controllers;

[ApiController]
[Route("api/v1/notifications")]
[Authorize]
public sealed class NotificationsController(DareCareDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> MyNotifications([FromQuery] bool unreadOnly = false, CancellationToken ct = default)
    {
        var uid = User.CurrentUserId();
        var query = db.Notifications.AsNoTracking().Where(n => n.RecipientUserId == uid);
        if (unreadOnly) query = query.Where(n => n.ReadAtUtc == null);
        var items = await query
            .OrderByDescending(n => n.CreatedAtUtc).Take(100)
            .Select(n => new { n.Id, n.Title, n.Message, n.ReferenceType, n.ReferenceId,
                n.CreatedAtUtc, n.ReadAtUtc })
            .ToListAsync(ct);
        return Ok(items);
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> UnreadCount(CancellationToken ct)
    {
        var uid = User.CurrentUserId();
        return Ok(new { unreadCount = await db.Notifications.CountAsync(n =>
            n.RecipientUserId == uid && n.ReadAtUtc == null, ct) });
    }

    [HttpPatch("read-all")]
    public async Task<IActionResult> ReadAll(CancellationToken ct)
    {
        var uid = User.CurrentUserId();
        var now = DateTime.UtcNow;
        var updated = await db.Notifications
            .Where(n => n.RecipientUserId == uid && n.ReadAtUtc == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(n => n.ReadAtUtc, now), ct);
        return Ok(new { markedRead = updated });
    }

    [HttpPatch("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct)
    {
        var uid = User.CurrentUserId();
        var item = await db.Notifications.SingleOrDefaultAsync(n => n.Id == id && n.RecipientUserId == uid, ct);
        if (item is null) return NotFound();
        if (item.ReadAtUtc is null)
        {
            item.ReadAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
        return NoContent();
    }
}

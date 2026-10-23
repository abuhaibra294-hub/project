using System.Net;
using DareCare.Api.Services;
using DareCare.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DareCare.Api.Controllers;

[ApiController]
[Route("api/v1/dev/mailbox")]
[Authorize(Roles = AppRoles.Admin)]
public sealed class DevelopmentMailboxController(
    IWebHostEnvironment environment, DevelopmentEmailOutbox outbox) : ControllerBase
{
    [HttpGet]
    public IActionResult List()
    {
        // Do not expose one-time codes in staging, production, or non-local access.
        if (!environment.IsDevelopment() ||
            HttpContext.Connection.RemoteIpAddress is not { } ip || !IPAddress.IsLoopback(ip))
            return NotFound();
        return Ok(outbox.Snapshot());
    }
}

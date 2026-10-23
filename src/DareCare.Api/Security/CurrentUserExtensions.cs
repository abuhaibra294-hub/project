using System.Security.Claims;

namespace DareCare.Api.Security;

public static class CurrentUserExtensions
{
    public static Guid CurrentUserId(this ClaimsPrincipal user)
    {
        var subject = user.FindFirstValue("sub");
        if (!Guid.TryParse(subject, out var id))
            throw new UnauthorizedAccessException("Authenticated user id is missing.");
        return id;
    }
}

using DareCare.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DareCare.Api.Security;

// Uses the existing Identity token table in both SQL Server and SQLite.
// Compare-and-swap prevents two concurrent requests from consuming the same state.
public sealed class AuthStateStore(DareCareDbContext db)
{
    private const string Provider = "DareCare.Auth";

    private IQueryable<IdentityUserToken<Guid>> Row(Guid userId, string name) =>
        db.Set<IdentityUserToken<Guid>>()
            .Where(t => t.UserId == userId && t.LoginProvider == Provider && t.Name == name);

    public Task<string?> ReadAsync(Guid userId, string name, CancellationToken ct) =>
        Row(userId, name).Select(t => t.Value).SingleOrDefaultAsync(ct);

    public async Task<bool> TryWriteAsync(Guid userId, string name, string? previous, string value, CancellationToken ct)
    {
        if (previous is not null)
            return await Row(userId, name).Where(t => t.Value == previous)
                .ExecuteUpdateAsync(t => t.SetProperty(x => x.Value, value), ct) == 1;

        var token = new IdentityUserToken<Guid>
        {
            UserId = userId, LoginProvider = Provider, Name = name, Value = value
        };
        db.Add(token);
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException ex) when (DbConflict.IsUniqueViolation(ex))
        {
            return false;
        }
        finally
        {
            db.Entry(token).State = EntityState.Detached;
        }
    }

    public async Task<bool> TryDeleteAsync(Guid userId, string name, string previous, CancellationToken ct) =>
        await Row(userId, name).Where(t => t.Value == previous).ExecuteDeleteAsync(ct) == 1;
}

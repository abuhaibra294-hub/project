using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DareCare.Infrastructure.Persistence;

/// <summary>
/// The EF tooling must always scaffold SQL Server migrations, even when the
/// default API staging settings opt into the disposable SQLite demo.
/// A connection is opened only by commands such as "database update".
/// </summary>
public sealed class DareCareDesignTimeDbContextFactory : IDesignTimeDbContextFactory<DareCareDbContext>
{
    public DareCareDbContext CreateDbContext(string[] args)
    {
        // The fallback is a deliberately unusable design-only connection string.
        // Generating migrations does not connect to it. Applying migrations to
        // a real database requires an explicit, secret connection string.
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
            connectionString = "Server=127.0.0.1;Database=DareCareDesignTimeOnly;" +
                "User Id=DESIGN_ONLY_NO_LOGIN;Password=NotARealConnection!42;" +
                "Encrypt=True;TrustServerCertificate=False";

        var options = new DbContextOptionsBuilder<DareCareDbContext>()
            .UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure(
                maxRetryCount: 4, maxRetryDelay: TimeSpan.FromSeconds(5), errorNumbersToAdd: null))
            .Options;
        return new DareCareDbContext(options);
    }
}

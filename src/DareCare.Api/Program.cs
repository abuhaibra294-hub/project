using System.Text;
using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using DareCare.Api.Security;
using DareCare.Api.Services;
using DareCare.Infrastructure.Identity;
using DareCare.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// SQLite remains a local Development database by default. A disposable, synthetic-data-only
// Render demo may explicitly opt in under Staging; Production must always use SQL Server.
var demoSqlite = builder.Environment.IsStaging() &&
    builder.Configuration.GetValue<bool>("Demo:AllowStagingSqlite");
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var databaseProvider = builder.Configuration["Database:Provider"]?.Trim() ??
    (demoSqlite ? "Sqlite" : "SqlServer");
// This *disposable demo branch* is allowed to start without an external SQL Server.
// If Render has stale SQL Server defaults but no connection string, respect the
// explicit Staging demo opt-in and use temporary SQLite instead.
if (demoSqlite && databaseProvider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase) &&
    string.IsNullOrWhiteSpace(connectionString))
{
    Console.WriteLine("DareCare disposable Staging demo: no SQL Server connection configured; selecting temporary SQLite.");
    databaseProvider = "Sqlite";
}
Console.WriteLine($"DareCare startup: environment={builder.Environment.EnvironmentName}, databaseProvider={databaseProvider}, disposableDemo={demoSqlite}");
var useSqlite = databaseProvider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase);
if (useSqlite && !builder.Environment.IsDevelopment() && !demoSqlite)
    throw new InvalidOperationException("SQLite is allowed only in Development or opted-in Staging demo; Production requires SQL Server.");
if (!useSqlite && !databaseProvider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
    throw new InvalidOperationException("Database:Provider must be SqlServer or Sqlite.");
if (useSqlite)
{
    // /tmp is writable by the non-root Docker user. Free Render loses all data on restart.
    // If you later attach a paid persistent disk, set Demo:SqliteDirectory to /data.
    var dataDir = demoSqlite
        ? builder.Configuration["Demo:SqliteDirectory"] ?? Path.Combine(Path.GetTempPath(), "darecare-demo")
        : Path.Combine(builder.Environment.ContentRootPath, "App_Data");
    if (!Path.IsPathFullyQualified(dataDir))
        throw new InvalidOperationException("Demo:SqliteDirectory must be an absolute path.");
    Directory.CreateDirectory(dataDir);
    connectionString = builder.Configuration.GetConnectionString("SqliteConnection")
        ?? $"Data Source={Path.Combine(dataDir, demoSqlite ? "darecare-demo.db" : "darecare-sprint5-dev.db")}";
}
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("Set ConnectionStrings:DefaultConnection for SQL Server using user-secrets.");
var secret = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(secret) || Encoding.UTF8.GetByteCount(secret) < 32)
    throw new InvalidOperationException("Jwt:Key must be a secret of at least 32 UTF-8 bytes, supplied through user-secrets / environment.");
if (builder.Configuration.GetValue<int>("Jwt:ExpiryMinutes", 15) is < 5 or > 60)
    throw new InvalidOperationException("Jwt:ExpiryMinutes must be between 5 and 60.");
if (builder.Configuration.GetValue<int>("Auth:RefreshTokenDays", 14) is < 1 or > 30)
    throw new InvalidOperationException("Auth:RefreshTokenDays must be between 1 and 30.");
// Explicit setting wins; by default production requires mailbox verification.
builder.Configuration["Auth:RequireConfirmedEmail"] ??= builder.Environment.IsDevelopment() ? "false" : "true";
var requireEmailConfirmation = builder.Configuration.GetValue<bool>("Auth:RequireConfirmedEmail");

builder.Services.AddDbContext<DareCareDbContext>(options =>
{
    if (useSqlite) options.UseSqlite(connectionString);
    else options.UseSqlServer(connectionString);
});
builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 12;
        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.AllowedForNewUsers = true;
    })
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<DareCareDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddScoped<IPrivatePrescriptionStorage, PrivatePrescriptionStorage>();
builder.Services.AddSingleton<DevelopmentEmailOutbox>();
builder.Services.AddScoped<IAccountEmailService, AccountEmailService>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "sub",
            RoleClaimType = "role"
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                if (!Guid.TryParse(context.Principal?.FindFirstValue("sub"), out var id) ||
                    !int.TryParse(context.Principal?.FindFirstValue("token_version"), out var tokenVersion))
                {
                    context.Fail("Invalid account claim.");
                    return;
                }
                var db = context.HttpContext.RequestServices.GetRequiredService<DareCareDbContext>();
                var user = await db.Users.AsNoTracking().Where(u => u.Id == id)
                    .Select(u => new { u.IsActive, u.IsPendingInvitation, u.EmailConfirmed, u.TokenVersion })
                    .SingleOrDefaultAsync(context.HttpContext.RequestAborted);
                if (user is null || !user.IsActive || user.IsPendingInvitation ||
                    user.TokenVersion != tokenVersion ||
                    (requireEmailConfirmation && !user.EmailConfirmed))
                    context.Fail("Session expired or account disabled.");
            }
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
// Explicit, exact browser origins only. Reject wildcard/invalid origins early instead of
// making a deployed patient-data API accessible from untrusted websites.
foreach (var origin in origins)
{
    if (!Uri.TryCreate(origin, UriKind.Absolute, out var parsed) ||
        parsed.AbsolutePath != "/" || !string.IsNullOrEmpty(parsed.Query) ||
        !string.IsNullOrEmpty(parsed.Fragment) ||
        (parsed.Scheme != Uri.UriSchemeHttps &&
         !(builder.Environment.IsDevelopment() && parsed.Scheme == Uri.UriSchemeHttp && parsed.IsLoopback)))
        throw new InvalidOperationException("Cors:AllowedOrigins must contain exact HTTPS origins (HTTP localhost only for Development).");
}
builder.Services.AddCors(options => options.AddPolicy("clients", policy =>
{
    if (origins.Length > 0) policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
}));
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    // Per-account throttling for user-generated support content.
    options.AddPolicy("support-write", context => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: context.User.FindFirstValue("sub") ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 6,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        }));
    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        }));
});

var app = builder.Build();
// Production never exposes API metadata. Staging may opt in for integration testing.
var enableDocs = app.Environment.IsDevelopment() ||
    (app.Environment.IsStaging() && app.Configuration.GetValue<bool>("ApiDocs:EnableInStaging"));
if (enableDocs)
{
    app.MapOpenApi();
    // The explorer is deliberately mapped as a single file. This does NOT serve
    // Development-only wwwroot/auth.html or the test mailbox on a public staging host.
    app.MapGet("/docs", () => Results.File(
        Path.Combine(app.Environment.ContentRootPath, "wwwroot", "api-docs.html"),
        "text/html; charset=utf-8"));
}
if (app.Environment.IsDevelopment())
{
    // This manual API testing page is never exposed outside Development.
    app.UseStaticFiles();
    app.MapGet("/dev/auth", () => Results.Redirect("/auth.html"));
}
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler(); // RFC 7807 generic errors, no exception detail in deployed responses.
    app.UseHsts();
}
app.UseHttpsRedirection();
// Responses containing account data or bearer tokens must never be cached.
app.Use(async (ctx, next) =>
{
    if (ctx.Request.Path.StartsWithSegments("/api/v1"))
    {
        ctx.Response.Headers.CacheControl = "no-store";
        ctx.Response.Headers.Pragma = "no-cache";
    }
    await next();
});
app.UseRouting();
app.UseCors("clients");
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();
app.MapControllers();
// Liveness indicates that Kestrel is running; readiness verifies the real database.
app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy", service = "DareCare.Api", databaseProvider = useSqlite ? "Sqlite" : "SqlServer"
}));
app.MapGet("/health/ready", async (DareCareDbContext db, CancellationToken ct) =>
{
    try
    {
        if (await db.Database.CanConnectAsync(ct) &&
            (useSqlite || !(await db.Database.GetPendingMigrationsAsync(ct)).Any()))
            return Results.Ok(new { status = "ready", service = "DareCare.Api" });
    }
    catch (Exception) when (!ct.IsCancellationRequested) { /* Do not leak connection strings. */ }
    return Results.Json(new { status = "unavailable", service = "DareCare.Api" },
        statusCode: StatusCodes.Status503ServiceUnavailable);
});

// SQLite is initialized for local Development or disposable Staging demos only. SQL Server uses explicit migrations.
// EnsureCreated is intentionally NOT used for SQL Server, where schema evolution matters.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DareCareDbContext>();
    if (useSqlite)
        await db.Database.EnsureCreatedAsync();
    else
    {
        // Never modify a persistent SQL Server database from API startup.
        // Migrations must be explicitly reviewed and applied by a deployment
        // operator before bringing this API online.
        var pendingMigrations = (await db.Database.GetPendingMigrationsAsync()).ToArray();
        if (pendingMigrations.Length != 0)
            throw new InvalidOperationException(
                "SQL Server schema is not ready: apply the checked-in EF Core migrations " +
                "before starting DareCare. Pending migrations: " +
                string.Join(", ", pendingMigrations));
    }
    // A new Staging SQL database has no Admin; allow a ONE-TIME opt-in bootstrap.
    // Remove AllowOneTimeStaging + credentials after the first successful initialization.
    // Production always ignores this option to avoid accidental account creation.
    var allowBootstrap = app.Environment.IsDevelopment() ||
        (app.Environment.IsStaging() && builder.Configuration.GetValue<bool>("BootstrapAdmin:AllowOneTimeStaging"));
    await InitialDataSeeder.SeedAsync(scope.ServiceProvider,
        allowBootstrap ? builder.Configuration["BootstrapAdmin:Email"] : null,
        allowBootstrap ? builder.Configuration["BootstrapAdmin:Password"] : null);
}
await app.RunAsync();

// Expose Program for future WebApplicationFactory integration tests.
public partial class Program { }

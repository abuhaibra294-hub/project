using DareCare.Api.Security;
using DareCare.Api.Services;
using DareCare.Domain;
using DareCare.Infrastructure.Identity;
using DareCare.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DareCare.Api.Tests;

public sealed class AuthFactorTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    [Fact]
    public async Task EmailCode_IsPurposeBound_Expires_AndStopsAfterFiveFailures()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(Ct);
        await using var db = new DareCareDbContext(new DbContextOptionsBuilder<DareCareDbContext>()
            .UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync(Ct);

        var user = NewUser("otp@example.test");
        db.Users.Add(user);
        await db.SaveChangesAsync(Ct);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["Jwt:Key"] = "Synthetic test key for email code hashing" }).Build();
        var mailbox = new Mailbox();
        var clock = new TestClock();
        var otp = new EmailOtpService(new AuthStateStore(db), mailbox, config, clock);
        const EmailOtpPurpose verify = EmailOtpPurpose.VerifyEmail;

        await otp.SendAsync(user, verify, Ct);
        var code = Assert.Single(mailbox.Codes);
        await otp.SendAsync(user, verify, Ct);
        Assert.Single(mailbox.Codes);
        Assert.False(await otp.TryConsumeAsync(user, EmailOtpPurpose.ResetPassword, code, Ct));

        var wrong = code == "000000" ? "000001" : "000000";
        for (var i = 0; i < 5; i++)
            Assert.False(await otp.TryConsumeAsync(user, verify, wrong, Ct));
        Assert.False(await otp.TryConsumeAsync(user, verify, code, Ct));

        clock.Now = clock.Now.AddMinutes(2);
        await otp.SendAsync(user, verify, Ct);
        code = mailbox.Codes[^1];
        clock.Now = clock.Now.AddMinutes(10);
        Assert.False(await otp.TryConsumeAsync(user, verify, code, Ct));
        await otp.SendAsync(user, verify, Ct);
        code = mailbox.Codes[^1];
        Assert.True(await otp.TryConsumeAsync(user, verify, code, Ct));
        Assert.False(await otp.TryConsumeAsync(user, verify, code, Ct));
    }

    [Fact]
    public async Task GoogleAccount_UsesSubjectAndCreatesPatientWithoutSilentlyLinkingEmails()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(Ct);
        var container = new ServiceCollection();
        container.AddLogging();
        container.AddDbContext<DareCareDbContext>(options => options.UseSqlite(connection));
        container.AddIdentityCore<ApplicationUser>(options => options.User.RequireUniqueEmail = true)
            .AddRoles<IdentityRole<Guid>>().AddEntityFrameworkStores<DareCareDbContext>();
        await using var services = container.BuildServiceProvider();
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DareCareDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        await db.Database.EnsureCreatedAsync(Ct);
        Assert.True((await roles.CreateAsync(new IdentityRole<Guid>(AppRoles.Patient))).Succeeded);
        Assert.True((await roles.CreateAsync(new IdentityRole<Guid>(AppRoles.Admin))).Succeeded);
        var admin = NewUser("admin@gmail.com");
        var patient = NewUser("patient@gmail.com");
        Assert.True((await users.CreateAsync(admin)).Succeeded);
        Assert.True((await users.CreateAsync(patient)).Succeeded);
        Assert.True((await users.AddToRoleAsync(admin, AppRoles.Admin)).Succeeded);
        Assert.True((await users.AddToRoleAsync(patient, AppRoles.Patient)).Succeeded);

        var google = new GoogleAccountService(users, db);
        var claims = new GoogleIdentity("synthetic-subject", patient.Email!, null);
        var existing = await google.FindOrCreatePatientAsync(claims, Ct);
        Assert.Null(existing.User);
        Assert.True(existing.LinkRequired);
        Assert.False(await google.LinkAsync(admin, claims with { Email = admin.Email! }));
        Assert.True(await google.LinkAsync(patient, claims));
        Assert.Equal(patient.Id, (await google.FindOrCreatePatientAsync(claims, Ct)).User?.Id);

        var newClaims = new GoogleIdentity("new-subject", "new-patient@gmail.com", "New Patient");
        var created = Assert.IsType<ApplicationUser>((await google.FindOrCreatePatientAsync(newClaims, Ct)).User);
        Assert.True(created.EmailConfirmed);
        Assert.True(await google.IsPatientAsync(created));
        Assert.Single(await db.Patients.Where(row => row.UserId == created.Id).ToListAsync(Ct));
        Assert.Equal(created.Id, (await google.FindOrCreatePatientAsync(newClaims, Ct)).User?.Id);
    }

    private static ApplicationUser NewUser(string email) => new()
    {
        Id = Guid.NewGuid(), Email = email, UserName = email, FullName = "Synthetic User", EmailConfirmed = true
    };

    private sealed class TestClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UnixEpoch;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class Mailbox : IAccountEmailService
    {
        public bool IsConfigured => true;
        public List<string> Codes { get; } = [];
        public Task SendOtpAsync(ApplicationUser user, string code, EmailOtpPurpose purpose, int lifetimeMinutes, CancellationToken ct)
        {
            Codes.Add(code);
            return Task.CompletedTask;
        }

        public Task SendVerificationAsync(ApplicationUser user, CancellationToken ct) => throw new NotSupportedException();
        public Task SendPasswordResetAsync(ApplicationUser user, CancellationToken ct) => throw new NotSupportedException();
    }
}

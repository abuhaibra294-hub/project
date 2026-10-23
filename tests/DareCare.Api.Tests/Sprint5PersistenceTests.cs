using DareCare.Domain;
using DareCare.Domain.Entities;
using DareCare.Infrastructure.Identity;
using DareCare.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DareCare.Api.Tests;

public sealed class Sprint5PersistenceTests
{
    private static (SqliteConnection Connection, DareCareDbContext Db) OpenDb()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var db = new DareCareDbContext(new DbContextOptionsBuilder<DareCareDbContext>()
            .UseSqlite(connection).Options);
        db.Database.EnsureCreated();
        return (connection, db);
    }

    private static ApplicationUser User(string email) => new()
    {
        Id = Guid.NewGuid(), UserName = email, Email = email,
        FullName = "Synthetic Test User", EmailConfirmed = true
    };

    [Fact]
    public void Patient_CannotHaveTwoDefaultAddresses()
    {
        var (connection, db) = OpenDb();
        using (connection) using (db)
        {
            var patient = User("a@tests.local");
            db.Users.Add(patient);
            db.SaveChanges();
            db.PatientAddresses.Add(new PatientAddress
            {
                PatientUserId = patient.Id, Label = "Home", City = "Mansoura",
                FullAddress = "Street 1, Building A", IsDefault = true
            });
            db.SaveChanges();
            db.PatientAddresses.Add(new PatientAddress
            {
                PatientUserId = patient.Id, Label = "Work", City = "Mansoura",
                FullAddress = "Street 2, Building B", IsDefault = true
            });
            Assert.Throws<DbUpdateException>(() => db.SaveChanges());
        }
    }

    [Fact]
    public void SameNurse_CannotBeFavoritedTwiceBySamePatient()
    {
        var (connection, db) = OpenDb();
        using (connection) using (db)
        {
            var patient = User("p@tests.local");
            var nurseUser = User("n@tests.local");
            db.Users.AddRange(patient, nurseUser);
            var nurse = new NurseProfile { UserId = nurseUser.Id, LicenseNumber = "TEST-S5" };
            db.Nurses.Add(nurse);
            db.SaveChanges();
            db.FavoriteNurses.Add(new FavoriteNurse
                { PatientUserId = patient.Id, NurseProfileId = nurse.Id });
            db.SaveChanges();
            // A different request/context must not bypass the database composite key.
            using var secondDb = new DareCareDbContext(new DbContextOptionsBuilder<DareCareDbContext>()
                .UseSqlite(connection).Options);
            secondDb.FavoriteNurses.Add(new FavoriteNurse
                { PatientUserId = patient.Id, NurseProfileId = nurse.Id });
            Assert.Throws<DbUpdateException>(() => secondDb.SaveChanges());
        }
    }

    [Fact]
    public void SupportTicket_PersistsItsPrivateConversation()
    {
        var (connection, db) = OpenDb();
        using (connection) using (db)
        {
            var patient = User("support@tests.local");
            db.Users.Add(patient);
            var ticket = SupportTicket.Create(patient.Id, SupportTicketCategory.Account, "Account access problem");
            db.SupportTickets.Add(ticket);
            db.SupportMessages.Add(SupportMessage.Create(ticket.Id, patient.Id, "Please help with account", false));
            db.SaveChanges();
            db.ChangeTracker.Clear();
            var loaded = db.SupportTickets.Include(x => x.Messages).Single(x => x.Id == ticket.Id);
            Assert.Equal(SupportTicketStatus.Open, loaded.Status);
            Assert.Single(loaded.Messages);
            Assert.Equal("Please help with account", loaded.Messages[0].Body);
        }
    }
}

using DareCare.Domain;
using DareCare.Domain.Entities;
using DareCare.Infrastructure.Identity;
using DareCare.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DareCare.Api.Tests;

// Exercises the SQLite schema, relationships and unique review constraint.
// This test requires dotnet test; it was not runnable in the packaging container.
public sealed class ReviewPersistenceTests
{
    [Fact]
    public void OneCompletedBooking_CannotHaveTwoReviews()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<DareCareDbContext>()
            .UseSqlite(connection).Options;
        using var db = new DareCareDbContext(options);
        db.Database.EnsureCreated();

        var patientUser = new ApplicationUser
        {
            Id = Guid.NewGuid(), UserName = "patient@test.local", Email = "patient@test.local",
            FullName = "Synthetic Patient", EmailConfirmed = true
        };
        var nurseUser = new ApplicationUser
        {
            Id = Guid.NewGuid(), UserName = "nurse@test.local", Email = "nurse@test.local",
            FullName = "Synthetic Nurse", EmailConfirmed = true
        };
        db.Users.AddRange(patientUser, nurseUser);
        var nurse = new NurseProfile
        {
            Id = Guid.NewGuid(), UserId = nurseUser.Id,
            LicenseNumber = "TEST-LICENSE", IsApproved = true
        };
        var skill = new Skill { Name = "Synthetic Care" };
        var service = new CareService
        {
            Name = "Synthetic Service", Description = "Test only",
            RequiredSkill = skill, DurationMinutes = 60, BasePrice = 100m
        };
        var slot = new AvailabilitySlot
        {
            NurseProfileId = nurse.Id,
            StartsAtUtc = DateTime.UtcNow.AddDays(1),
            EndsAtUtc = DateTime.UtcNow.AddDays(1).AddHours(2)
        };
        db.Nurses.Add(nurse);
        db.CareServices.Add(service);
        db.AvailabilitySlots.Add(slot);
        db.SaveChanges();

        var booking = new Booking
        {
            PatientUserId = patientUser.Id, NurseProfileId = nurse.Id,
            CareServiceId = service.Id, AvailabilitySlotId = slot.Id,
            ServiceAddress = "Synthetic Address", PriceAtBooking = 100m
        };
        db.Bookings.Add(booking);
        db.SaveChanges();
        booking.TransitionTo(BookingStatus.Accepted);
        booking.TransitionTo(BookingStatus.InProgress);
        booking.TransitionTo(BookingStatus.Completed);
        db.SaveChanges();

        db.NurseReviews.Add(NurseReview.Create(booking.Id, patientUser.Id, nurse.Id, 5, "Great"));
        db.SaveChanges();
        // Clear the tracked one-to-one navigation to test the database unique index
        // rather than EF Core's in-memory required relationship fixup.
        db.ChangeTracker.Clear();
        db.NurseReviews.Add(NurseReview.Create(booking.Id, patientUser.Id, nurse.Id, 1, null));
        Assert.Throws<DbUpdateException>(() => db.SaveChanges());
    }
}

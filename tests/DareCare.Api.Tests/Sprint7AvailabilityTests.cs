using System.Security.Claims;
using DareCare.Api.Controllers;
using DareCare.Application.Contracts;
using DareCare.Domain;
using DareCare.Domain.Entities;
using DareCare.Infrastructure.Identity;
using DareCare.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DareCare.Api.Tests;

public sealed class Sprint7AvailabilityTests
{
    private static ClaimsPrincipal Principal(Guid id) => new(
        new ClaimsIdentity(
            [new Claim("sub", id.ToString()), new Claim(ClaimTypes.Role, AppRoles.Nurse)],
            "TestAuth"));

    private static ApplicationUser TestUser(string name) => new()
    {
        Id = Guid.NewGuid(),
        UserName = name,
        Email = name,
        FullName = "Synthetic user",
        EmailConfirmed = true
    };

    [Fact]
    public async Task OverlappingSlot_IsRejected_ButAdjacentSlotIsAllowed()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        using var db = new DareCareDbContext(new DbContextOptionsBuilder<DareCareDbContext>()
            .UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();

        var user = TestUser("slot-nurse@tests.local");
        var nurse = new NurseProfile { UserId = user.Id, LicenseNumber = "S7-NURSE", IsApproved = true };
        db.Users.Add(user);
        db.Nurses.Add(nurse);
        await db.SaveChangesAsync();

        var controller = new NursesController(db)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = Principal(user.Id) }
            }
        };
        var start = DateTimeOffset.UtcNow.AddHours(2);
        Assert.IsType<CreatedResult>(await controller.AddSlot(new CreateAvailabilitySlotRequest
        {
            StartsAt = start,
            EndsAt = start.AddHours(1)
        }, CancellationToken.None));

        Assert.IsType<ConflictObjectResult>(await controller.AddSlot(new CreateAvailabilitySlotRequest
        {
            StartsAt = start.AddMinutes(30),
            EndsAt = start.AddHours(1).AddMinutes(30)
        }, CancellationToken.None));

        Assert.IsType<CreatedResult>(await controller.AddSlot(new CreateAvailabilitySlotRequest
        {
            StartsAt = start.AddHours(1),
            EndsAt = start.AddHours(2)
        }, CancellationToken.None));
        Assert.Equal(2, await db.AvailabilitySlots.AsNoTracking()
            .CountAsync(s => s.NurseProfileId == nurse.Id));
    }

    [Fact]
    public async Task AnActiveBookingOwnsItsSlot_UntilRejected()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        using var db = new DareCareDbContext(new DbContextOptionsBuilder<DareCareDbContext>()
            .UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();

        var nurseUser = TestUser("reserved-nurse@tests.local");
        var patientA = TestUser("patient-a@tests.local");
        var patientB = TestUser("patient-b@tests.local");
        db.Users.AddRange(nurseUser, patientA, patientB);
        var nurse = new NurseProfile { UserId = nurseUser.Id, LicenseNumber = "S7-BOOK-NURSE" };
        var skill = new Skill { Name = "Synthetic test skill" };
        db.Nurses.Add(nurse);
        db.Skills.Add(skill);
        var service = new CareService
        {
            Name = "Synthetic visit",
            Description = "Synthetic booking test",
            DurationMinutes = 60,
            BasePrice = 100,
            RequiredSkillId = skill.Id
        };
        var slot = new AvailabilitySlot
        {
            NurseProfileId = nurse.Id,
            StartsAtUtc = DateTime.UtcNow.AddHours(3),
            EndsAtUtc = DateTime.UtcNow.AddHours(4)
        };
        db.CareServices.Add(service);
        db.AvailabilitySlots.Add(slot);
        await db.SaveChangesAsync();

        var original = new Booking
        {
            PatientUserId = patientA.Id,
            NurseProfileId = nurse.Id,
            AvailabilitySlotId = slot.Id,
            CareServiceId = service.Id,
            ServiceAddress = "Synthetic address 123",
            PriceAtBooking = service.BasePrice
        };
        db.Bookings.Add(original);
        await db.SaveChangesAsync();

        var second = new Booking
        {
            PatientUserId = patientB.Id,
            NurseProfileId = nurse.Id,
            AvailabilitySlotId = slot.Id,
            CareServiceId = service.Id,
            ServiceAddress = "Synthetic address 456",
            PriceAtBooking = service.BasePrice
        };
        db.Bookings.Add(second);
        await Assert.ThrowsAsync<DbUpdateException>(async () => { await db.SaveChangesAsync(); });
        db.ChangeTracker.Clear();

        var saved = await db.Bookings.SingleAsync(b => b.Id == original.Id);
        saved.TransitionTo(BookingStatus.Rejected);
        await db.SaveChangesAsync();
        db.Bookings.Add(second);
        await db.SaveChangesAsync();
        Assert.Equal(2, await db.Bookings.CountAsync(b => b.AvailabilitySlotId == slot.Id));
    }
}

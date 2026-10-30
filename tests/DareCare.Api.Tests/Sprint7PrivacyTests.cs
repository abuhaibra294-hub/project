using System.Security.Claims;
using DareCare.Api.Controllers;
using DareCare.Api.Services;
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

public sealed class Sprint7PrivacyTests
{
    private static ClaimsPrincipal Principal(Guid id, string role) => new(
        new ClaimsIdentity(
            [new Claim("sub", id.ToString()), new Claim(ClaimTypes.Role, role)],
            "TestAuth"));

    private static ApplicationUser User(string email) => new()
    {
        Id = Guid.NewGuid(),
        Email = email,
        UserName = email,
        FullName = "Synthetic user",
        EmailConfirmed = true
    };

    private static ControllerContext As(Guid id, string role) =>
        new() { HttpContext = new DefaultHttpContext { User = Principal(id, role) } };

    [Fact]
    public async Task PatientsAndPharmacies_CannotReadOtherPeoplesOrdersOrPrescriptions()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        using var db = new DareCareDbContext(new DbContextOptionsBuilder<DareCareDbContext>()
            .UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();

        var patient = User("order-owner@tests.local");
        var stranger = User("order-stranger@tests.local");
        var pharmacyUser = User("order-pharmacy@tests.local");
        var otherPharmacyUser = User("wrong-pharmacy@tests.local");
        db.Users.AddRange(patient, stranger, pharmacyUser, otherPharmacyUser);
        var pharmacy = new PharmacyProfile
        {
            UserId = pharmacyUser.Id, BusinessName = "Synthetic Pharmacy",
            LicenseNumber = "S7-PHARMACY-1", City = "Mansoura",
            Address = "Synthetic pharmacy address", IsApproved = true
        };
        var otherPharmacy = new PharmacyProfile
        {
            UserId = otherPharmacyUser.Id, BusinessName = "Another Pharmacy",
            LicenseNumber = "S7-PHARMACY-2", City = "Mansoura",
            Address = "Another pharmacy address", IsApproved = true
        };
        db.Pharmacies.AddRange(pharmacy, otherPharmacy);
        var prescription = new Prescription
        {
            PatientUserId = patient.Id,
            PharmacyProfileId = pharmacy.Id,
            OriginalFileName = "synthetic.pdf",
            ContentType = "application/pdf",
            StorageKey = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.bin",
            FileSizeBytes = 8
        };
        db.Prescriptions.Add(prescription);
        var order = new PharmacyOrder
        {
            PrescriptionId = prescription.Id,
            PatientUserId = patient.Id,
            PharmacyProfileId = pharmacy.Id,
            QuoteAmount = 50
        };
        db.PharmacyOrders.Add(order);
        await db.SaveChangesAsync();

        var strangerOrders = new PharmacyOrdersController(db) { ControllerContext = As(stranger.Id, AppRoles.Patient) };
        Assert.IsType<ForbidResult>(await strangerOrders.Get(order.Id, CancellationToken.None));
        var patientOrders = new PharmacyOrdersController(db) { ControllerContext = As(patient.Id, AppRoles.Patient) };
        Assert.IsType<OkObjectResult>(await patientOrders.Get(order.Id, CancellationToken.None));
        var wrongPharmacyOrders = new PharmacyOrdersController(db)
            { ControllerContext = As(otherPharmacyUser.Id, AppRoles.Pharmacy) };
        Assert.IsType<ForbidResult>(await wrongPharmacyOrders.Get(order.Id, CancellationToken.None));
        Assert.IsType<NotFoundResult>(await wrongPharmacyOrders.Quote(
            prescription.Id, new DareCare.Application.Contracts.QuotePrescriptionRequest { Amount = 60 }, CancellationToken.None));
        var correctPharmacyOrders = new PharmacyOrdersController(db)
            { ControllerContext = As(pharmacyUser.Id, AppRoles.Pharmacy) };
        Assert.IsType<OkObjectResult>(await correctPharmacyOrders.Get(order.Id, CancellationToken.None));

        var storage = new FailIfOpenedStorage();
        var otherPatientPrescription = new PrescriptionsController(db, storage)
            { ControllerContext = As(stranger.Id, AppRoles.Patient) };
        Assert.IsType<ForbidResult>(await otherPatientPrescription.Get(prescription.Id, CancellationToken.None));
        Assert.IsType<ForbidResult>(await otherPatientPrescription.Download(prescription.Id, CancellationToken.None));
        var otherPharmacyPrescription = new PrescriptionsController(db, storage)
            { ControllerContext = As(otherPharmacyUser.Id, AppRoles.Pharmacy) };
        Assert.IsType<ForbidResult>(await otherPharmacyPrescription.Download(prescription.Id, CancellationToken.None));
        Assert.Equal(0, storage.OpenCalls);

        var ownerPrescription = new PrescriptionsController(db, storage)
            { ControllerContext = As(patient.Id, AppRoles.Patient) };
        Assert.IsType<OkObjectResult>(await ownerPrescription.Get(prescription.Id, CancellationToken.None));
    }

    [Fact]
    public async Task ReadAllNotifications_ChangesOnlyCurrentUsersRows()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        using var db = new DareCareDbContext(new DbContextOptionsBuilder<DareCareDbContext>()
            .UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();

        var a = User("notification-a@tests.local");
        var b = User("notification-b@tests.local");
        db.Users.AddRange(a, b);
        db.Notifications.AddRange(
            new UserNotification { RecipientUserId = a.Id, Title = "A1", Message = "Test" },
            new UserNotification { RecipientUserId = a.Id, Title = "A2", Message = "Test" },
            new UserNotification { RecipientUserId = b.Id, Title = "B1", Message = "Private" });
        await db.SaveChangesAsync();

        var controller = new NotificationsController(db) { ControllerContext = As(a.Id, AppRoles.Patient) };
        Assert.IsType<OkObjectResult>(await controller.ReadAll(CancellationToken.None));
        Assert.Equal(2, await db.Notifications.AsNoTracking()
            .CountAsync(n => n.RecipientUserId == a.Id && n.ReadAtUtc != null));
        Assert.Equal(1, await db.Notifications.AsNoTracking()
            .CountAsync(n => n.RecipientUserId == b.Id && n.ReadAtUtc == null));

        var foreignNotificationId = await db.Notifications.AsNoTracking()
            .Where(n => n.RecipientUserId == b.Id).Select(n => n.Id).SingleAsync();
        Assert.IsType<NotFoundResult>(await controller.MarkRead(foreignNotificationId, CancellationToken.None));
    }

    private sealed class FailIfOpenedStorage : IPrivatePrescriptionStorage
    {
        public int OpenCalls { get; private set; }
        public Task<string?> StoreAsync(IFormFile file, CancellationToken ct) =>
            throw new NotSupportedException();
        public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken ct)
        {
            OpenCalls++;
            throw new InvalidOperationException("Unauthorized file storage was accessed.");
        }
        public void Delete(string storageKey) => throw new NotSupportedException();
    }
}

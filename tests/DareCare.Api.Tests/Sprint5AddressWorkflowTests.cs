using System.Security.Claims;
using DareCare.Api.Controllers;
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

public sealed class Sprint5AddressWorkflowTests
{
    [Fact]
    public async Task SwitchAndDeleteDefaultAddress_KeepsExactlyOneWhenAnotherExists()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        using var db = new DareCareDbContext(new DbContextOptionsBuilder<DareCareDbContext>()
            .UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var patientId = Guid.NewGuid();
        db.Users.Add(new ApplicationUser
        {
            Id = patientId, UserName = "addr@tests.local", Email = "addr@tests.local",
            FullName = "Synthetic Patient", EmailConfirmed = true
        });
        var home = new PatientAddress { PatientUserId = patientId, IsDefault = true,
            Label = "Home", City = "Mansoura", FullAddress = "Road 10, building 1" };
        var work = new PatientAddress { PatientUserId = patientId, IsDefault = false,
            Label = "Work", City = "Mansoura", FullAddress = "Road 20, building 2" };
        db.PatientAddresses.AddRange(home, work);
        await db.SaveChangesAsync();

        var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("sub", patientId.ToString()), new Claim(ClaimTypes.Role, AppRoles.Patient)], "Test")) };
        var controller = new PatientAddressesController(db)
            { ControllerContext = new ControllerContext { HttpContext = http } };

        Assert.IsType<OkObjectResult>(await controller.SelectDefault(work.Id, CancellationToken.None));
        db.ChangeTracker.Clear();
        Assert.Equal(1, await db.PatientAddresses.CountAsync(a => a.PatientUserId == patientId && a.IsDefault));
        Assert.True(await db.PatientAddresses.AnyAsync(a => a.Id == work.Id && a.IsDefault));

        Assert.IsType<NoContentResult>(await controller.Delete(work.Id, CancellationToken.None));
        db.ChangeTracker.Clear();
        Assert.Equal(1, await db.PatientAddresses.CountAsync(a => a.PatientUserId == patientId && a.IsDefault));
        Assert.True(await db.PatientAddresses.AnyAsync(a => a.Id == home.Id && a.IsDefault));
    }
}

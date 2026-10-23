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

public sealed class Sprint5OwnershipTests
{
    private static ClaimsPrincipal Principal(Guid userId) =>
        new(new ClaimsIdentity([new Claim("sub", userId.ToString()), new Claim("role", AppRoles.Patient)], "TestAuth"));

    [Fact]
    public async Task OtherPatient_CannotReadAddressOrSupportTicket()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<DareCareDbContext>().UseSqlite(connection).Options;
        using var db = new DareCareDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var owner = Guid.NewGuid();
        var stranger = Guid.NewGuid();
        db.Users.AddRange(
            new ApplicationUser { Id = owner, UserName = "p1@tests.local", Email = "p1@tests.local", FullName = "Owner" },
            new ApplicationUser { Id = stranger, UserName = "p2@tests.local", Email = "p2@tests.local", FullName = "Stranger" });
        var address = new PatientAddress
        {
            PatientUserId = owner, Label = "Home", City = "Mansoura",
            FullAddress = "Street 123, building 22", IsDefault = true
        };
        db.PatientAddresses.Add(address);
        var ticket = SupportTicket.Create(owner, SupportTicketCategory.Other, "Synthetic issue");
        db.SupportTickets.Add(ticket);
        await db.SaveChangesAsync();

        var http = new DefaultHttpContext { User = Principal(stranger) };
        var addresses = new PatientAddressesController(db)
            { ControllerContext = new ControllerContext { HttpContext = http } };
        var support = new SupportController(db)
            { ControllerContext = new ControllerContext { HttpContext = http } };

        Assert.IsType<NotFoundResult>(await addresses.One(address.Id, CancellationToken.None));
        Assert.IsType<ForbidResult>(await support.Get(ticket.Id, 1, 50, CancellationToken.None));
        var ownList = Assert.IsType<OkObjectResult>(await addresses.All(CancellationToken.None));
        Assert.Empty(Assert.IsAssignableFrom<System.Collections.IEnumerable>(ownList.Value)
            .Cast<object>());
    }
}

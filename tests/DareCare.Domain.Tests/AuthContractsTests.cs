using System.ComponentModel.DataAnnotations;
using DareCare.Application.Contracts;
using Xunit;

namespace DareCare.Domain.Tests;

public sealed class AuthContractsTests
{
    [Fact]
    public void LoginRejectsInvalidEmailAndEmptyPassword()
    {
        var dto = new LoginRequest { Email = "invalid", Password = "" };
        var results = new List<ValidationResult>();
        Assert.False(Validator.TryValidateObject(dto, new ValidationContext(dto), results, true));
        Assert.True(results.Count >= 2);
    }

    [Fact]
    public void PatientProfileRejectsExcessivelyLongAddress()
    {
        var dto = new UpdatePatientProfileRequest { DefaultAddress = new string('x', 401) };
        var results = new List<ValidationResult>();
        Assert.False(Validator.TryValidateObject(dto, new ValidationContext(dto), results, true));
    }

    [Fact]
    public void RefreshRequestRejectsMissingToken()
    {
        var dto = new RefreshRequest();
        var results = new List<ValidationResult>();
        Assert.False(Validator.TryValidateObject(dto, new ValidationContext(dto), results, true));
    }
}

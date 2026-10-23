using DareCare.Api.Services;
using Xunit;

namespace DareCare.Api.Tests;

public sealed class PrescriptionFileTests
{
    [Theory]
    [InlineData("report.pdf", "%PDF-1.7", true)]
    [InlineData("report.pdf", "not-pdf", false)]
    [InlineData("evil.svg", "%PDF-1.7", false)]
    [InlineData("evil.exe", "%PDF-1.7", false)]
    public void FileSignatureAndExtensionMustAgree(string name, string header, bool valid) =>
        Assert.Equal(valid, PrivatePrescriptionStorage.IsSupportedHeader(name, System.Text.Encoding.UTF8.GetBytes(header)));

    [Fact]
    public void PngSignatureIsAccepted()
    {
        var bytes = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 };
        Assert.True(PrivatePrescriptionStorage.IsSupportedHeader("image.png", bytes));
        Assert.False(PrivatePrescriptionStorage.IsSupportedHeader("image.jpg", bytes));
    }
}

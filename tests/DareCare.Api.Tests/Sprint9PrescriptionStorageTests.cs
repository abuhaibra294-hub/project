using System.Text;
using DareCare.Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Xunit;

namespace DareCare.Api.Tests;

public sealed class Sprint9PrescriptionStorageTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "darecare-s9-storage-" + Guid.NewGuid().ToString("N"));
    private string PrivatePath => Path.Combine(_root, "private");

    private PrivatePrescriptionStorage Storage(string? overridePath = null)
    {
        var environment = new TestEnvironment
        {
            ContentRootPath = _root,
            WebRootPath = Path.Combine(_root, "wwwroot")
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Storage:PrivateDirectory"] = overridePath ?? PrivatePath
            }).Build();
        return new PrivatePrescriptionStorage(environment, config);
    }

    [Fact]
    public async Task ValidPdf_IsPrivateAndRoundTripsWithAnOpaqueKey()
    {
        var bytes = Encoding.ASCII.GetBytes("%PDF-1.7\nOnly synthetic CI content\n");
        var store = Storage();
        var file = new SyntheticFile(new MemoryStream(bytes), bytes.Length, "synthetic.pdf");
        var key = await store.StoreAsync(file, CancellationToken.None);

        Assert.NotNull(key);
        Assert.Matches("^[a-f0-9]{32}\\.bin$", key!);
        Assert.Single(Directory.GetFiles(PrivatePath));
        await using var opened = await store.OpenReadAsync(key!, CancellationToken.None);
        Assert.NotNull(opened);
        using var copied = new MemoryStream();
        await opened!.CopyToAsync(copied);
        Assert.Equal(bytes, copied.ToArray());

        store.Delete(key!);
        Assert.Empty(Directory.GetFiles(PrivatePath));
        Assert.Null(await store.OpenReadAsync(key!, CancellationToken.None));
    }

    [Fact]
    public async Task DeclaredLengthUnderreportsStream_RejectsAndDeletesPartialFile()
    {
        var bytes = Encoding.ASCII.GetBytes("%PDF-1.7\n" + new string('X', 100_000));
        var store = Storage();
        var file = new SyntheticFile(new MemoryStream(bytes), 12, "synthetic.pdf");

        Assert.Null(await store.StoreAsync(file, CancellationToken.None));
        Assert.Empty(Directory.GetFiles(PrivatePath));
    }

    [Fact]
    public async Task StreamFailureAfterAllDeclaredBytes_DoesNotLeaveOrphan()
    {
        var bytes = Encoding.ASCII.GetBytes("%PDF-1.7\nSynthetic failure test\n");
        var store = Storage();
        var file = new SyntheticFile(new ThrowsAtEndStream(bytes), bytes.Length, "synthetic.pdf");

        await Assert.ThrowsAsync<IOException>(async () =>
            await store.StoreAsync(file, CancellationToken.None));
        Assert.Empty(Directory.GetFiles(PrivatePath));
    }

    [Fact]
    public async Task SpoofedPdfHeader_IsRejectedWithoutWritingAFile()
    {
        var bytes = Encoding.ASCII.GetBytes("this is not a PDF");
        var store = Storage();
        var file = new SyntheticFile(new MemoryStream(bytes), bytes.Length, "spoofed.pdf");

        Assert.Null(await store.StoreAsync(file, CancellationToken.None));
        Assert.False(Directory.Exists(PrivatePath));
    }

    [Fact]
    public void StoragePathUnderPublicWebRoot_IsAlwaysRejected()
    {
        var publicDirectory = Path.Combine(_root, "wwwroot", "uploads");
        Assert.Throws<InvalidOperationException>(() => Storage(publicDirectory));
    }

    [Fact]
    public async Task TraversalStorageKeyCannotBeRead()
    {
        var store = Storage();
        Assert.Null(await store.OpenReadAsync("../../etc/passwd", CancellationToken.None));
        store.Delete("../../etc/passwd");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private sealed class ThrowsAtEndStream(byte[] bytes) : MemoryStream(bytes)
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            if (Position >= Length)
                throw new IOException("Synthetic source stream failed at EOF.");
            return base.ReadAsync(buffer, cancellationToken);
        }
    }

    private sealed class SyntheticFile(Stream stream, long declaredLength, string fileName) : IFormFile
    {
        public string ContentType { get; set; } = "application/pdf";
        public string ContentDisposition { get; set; } = "form-data";
        public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();
        public long Length => declaredLength;
        public string Name => "File";
        public string FileName => fileName;
        public Stream OpenReadStream() => stream;
        public void CopyTo(Stream target) => stream.CopyTo(target);
        public Task CopyToAsync(Stream target, CancellationToken cancellationToken = default) =>
            stream.CopyToAsync(target, cancellationToken);
    }

    private sealed class TestEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "DareCare.Api.Tests";
        public string EnvironmentName { get; set; } = "Staging";
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}

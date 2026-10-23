namespace DareCare.Api.Services;

public interface IPrivatePrescriptionStorage
{
    Task<string?> StoreAsync(IFormFile file, CancellationToken ct);
    Task<Stream?> OpenReadAsync(string storageKey, CancellationToken ct);
    void Delete(string storageKey);
}

public sealed class PrivatePrescriptionStorage(IWebHostEnvironment environment, IConfiguration configuration) : IPrivatePrescriptionStorage
{
    public const long MaxBytes = 8 * 1024 * 1024;
    // Staging must mount a persistent private volume; the default works only for local demos.
    private readonly string _directory = ResolveDirectory(environment, configuration);

    private static string ResolveDirectory(IWebHostEnvironment environment, IConfiguration configuration)
    {
        var configured = configuration["Storage:PrivateDirectory"];
        var candidate = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(environment.ContentRootPath, "App_Data", "prescriptions")
            : configured;
        if (!Path.IsPathFullyQualified(candidate))
            throw new InvalidOperationException("Storage:PrivateDirectory must be an absolute path.");
        var fullPath = Path.GetFullPath(candidate);
        if (!string.IsNullOrWhiteSpace(environment.WebRootPath))
        {
            var webRoot = Path.GetFullPath(environment.WebRootPath);
            if (fullPath.Equals(webRoot, StringComparison.OrdinalIgnoreCase) ||
                fullPath.StartsWith(webRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Private prescriptions cannot be stored under wwwroot.");
        }
        return fullPath;
    }

    // An extension check alone is not enough. Validate the uploaded file signature.
    public static bool IsSupportedHeader(string fileName, ReadOnlySpan<byte> header)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".pdf" => header.StartsWith("%PDF-"u8),
            ".png" => header.StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
            ".jpg" or ".jpeg" => header.StartsWith(new byte[] { 255, 216, 255 }),
            _ => false
        };
    }

    public static string? ContentTypeFor(string fileName) => Path.GetExtension(fileName).ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf",
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        _ => null
    };

    public async Task<string?> StoreAsync(IFormFile file, CancellationToken ct)
    {
        if (file.Length <= 0 || file.Length > MaxBytes || ContentTypeFor(file.FileName) is null)
            return null;
        await using var source = file.OpenReadStream();
        var header = new byte[8];
        var n = 0;
        while (n < header.Length)
        {
            var chunk = await source.ReadAsync(header.AsMemory(n), ct);
            if (chunk == 0) break;
            n += chunk;
        }
        if (!IsSupportedHeader(file.FileName, header.AsSpan(0, n))) return null;
        Directory.CreateDirectory(_directory);
        var key = $"{Guid.NewGuid():N}.bin"; // Server-controlled, never the untrusted upload name.
        var path = Path.Combine(_directory, key);
        try
        {
            await using var destination = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                81920, FileOptions.Asynchronous);
            await destination.WriteAsync(header.AsMemory(0, n), ct);
            await source.CopyToAsync(destination, ct);
            // Never rely on IFormFile.Length alone when verifying actual file bytes.
            if (destination.Length > MaxBytes || destination.Length != file.Length)
            {
                await destination.FlushAsync(ct);
                return null;
            }
            return key;
        }
        finally
        {
            // Remove invalid/partial uploads. Valid files are only persisted with DB metadata.
            if (File.Exists(path) && new FileInfo(path).Length != file.Length)
                File.Delete(path);
        }
    }

    public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken ct)
    {
        // Storage keys come from DB, but still reject traversal and unexpected formats.
        if (storageKey.Length != 36 || !storageKey.EndsWith(".bin", StringComparison.Ordinal) ||
            !Guid.TryParseExact(storageKey[..32], "N", out _))
            return Task.FromResult<Stream?>(null);
        var path = Path.Combine(_directory, storageKey);
        return Task.FromResult<Stream?>(File.Exists(path)
            ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous)
            : null);
    }

    public void Delete(string storageKey)
    {
        if (storageKey.Length != 36 || !storageKey.EndsWith(".bin", StringComparison.Ordinal) ||
            !Guid.TryParseExact(storageKey[..32], "N", out _)) return;
        var path = Path.Combine(_directory, storageKey);
        if (File.Exists(path)) File.Delete(path);
    }
}

using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace DareCare.Api.Security;

public static class RefreshTokenCodec
{
    // 48 bytes of CSPRNG entropy. Store the hash in SQL Server, hand the original to the client once.
    public static string Generate() => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(48));

    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    public static bool HasPlausibleFormat(string? token) =>
        token is { Length: >= 40 and <= 256 } &&
        token.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
}

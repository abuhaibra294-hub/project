using DareCare.Api.Security;
using Xunit;

namespace DareCare.Api.Tests;

public sealed class RefreshTokenCodecTests
{
    [Fact]
    public void GeneratedTokensAreUniqueAndWellFormed()
    {
        var one = RefreshTokenCodec.Generate();
        var two = RefreshTokenCodec.Generate();
        Assert.NotEqual(one, two);
        Assert.True(RefreshTokenCodec.HasPlausibleFormat(one));
        Assert.DoesNotContain('=', one);
    }

    [Fact]
    public void HashedTokenIsStableAndDoesNotRevealRawToken()
    {
        var token = RefreshTokenCodec.Generate();
        var hash = RefreshTokenCodec.Hash(token);
        Assert.Equal(64, hash.Length);
        Assert.Equal(hash, RefreshTokenCodec.Hash(token));
        Assert.NotEqual(hash, token);
    }

    [Theory]
    [InlineData("")]
    [InlineData("hello")]
    [InlineData("a b c d")]
    public void InvalidRefreshTokensAreRejected(string value)
    {
        Assert.False(RefreshTokenCodec.HasPlausibleFormat(value));
    }
}

using DareCare.Domain.Entities;
using Xunit;

namespace DareCare.Domain.Tests;

public sealed class NurseReviewTests
{
    private static NurseReview Review(int rating, string? comment = null) =>
        NurseReview.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), rating, comment);

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public void ValidRating_CreatesReview(int score) => Assert.Equal(score, Review(score).Rating);

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-1)]
    public void InvalidRating_IsRejected(int score) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Review(score));

    [Fact]
    public void Comment_IsTrimmedAndOptional()
    {
        Assert.Equal("Very good", Review(5, "  Very good  ").Comment);
        Assert.Null(Review(5, " ").Comment);
    }

    [Fact]
    public void ExcessivelyLongComment_IsRejected() =>
        Assert.Throws<ArgumentException>(() => Review(5, new string('x', 1001)));

    [Fact]
    public void MissingOwnershipIds_IsRejected() =>
        Assert.Throws<ArgumentException>(() => NurseReview.Create(Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), 5, null));
}

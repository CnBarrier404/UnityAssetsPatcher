using UnityAssetsPatcher.Domain.Integrity;
using Xunit;

namespace UnityAssetsPatcher.Domain.Tests.Integrity;

public sealed class FileIntegrityTests
{
    private const string ContentHash =
        "239f59ed55e737c77147cf55ad0c1b030b6d7ee748a7426952f9b852d5a935e5";

    [Fact]
    public void Constructor_WhenValuesAreValid_PreservesValues()
    {
        FileIntegrity integrity = new(7, ContentHash);

        Assert.Equal(7, integrity.Length);
        Assert.Equal(ContentHash, integrity.Sha256);
    }

    [Fact]
    public void Matches_WhenLengthAndHashAreEqual_ReturnsTrue()
    {
        FileIntegrity expected = new(7, ContentHash);
        FileIntegrity actual = new(7, ContentHash);

        bool matches = expected.Matches(actual);

        Assert.True(matches);
    }

    [Fact]
    public void Matches_WhenLengthDiffers_ReturnsFalse()
    {
        FileIntegrity expected = new(7, ContentHash);
        FileIntegrity actual = new(8, ContentHash);

        bool matches = expected.Matches(actual);

        Assert.False(matches);
    }

    [Fact]
    public void Matches_WhenHashDiffers_ReturnsFalse()
    {
        FileIntegrity expected = new(7, ContentHash);
        FileIntegrity actual = new(7, new string('0', FileIntegrity.Sha256HexLength));

        bool matches = expected.Matches(actual);

        Assert.False(matches);
    }

    [Fact]
    public void Constructor_WhenLengthIsNegative_ThrowsArgumentOutOfRangeException()
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new FileIntegrity(-1, new string('0', FileIntegrity.Sha256HexLength)));

        Assert.Equal("length", exception.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-sha256")]
    public void Constructor_WhenSha256IsInvalid_ThrowsArgumentException(string sha256)
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => new FileIntegrity(0, sha256));

        Assert.Equal("sha256", exception.ParamName);
    }

    [Fact]
    public void Constructor_WhenSha256ContainsUppercaseCharacters_ThrowsArgumentException()
    {
        string sha256 = new string('A', FileIntegrity.Sha256HexLength);

        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => new FileIntegrity(0, sha256));

        Assert.Equal("sha256", exception.ParamName);
    }

}

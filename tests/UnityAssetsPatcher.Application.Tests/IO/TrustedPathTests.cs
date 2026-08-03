using UnityAssetsPatcher.Application.IO;
using Xunit;

namespace UnityAssetsPatcher.Application.Tests.IO;

public sealed class TrustedPathTests
{
    [Fact]
    public void NormalizeAbsolutePath_WhenPathHasTrailingSeparator_TrimsIt()
    {
        string root = Path.GetTempPath();
        string path = Path.Combine(root, "game");

        string normalized = TrustedPath.NormalizeAbsolutePath(path + Path.DirectorySeparatorChar);

        Assert.Equal(path, normalized);
    }

    [Fact]
    public void NormalizeAbsolutePath_WhenPathIsRelative_ReturnsFullPath()
    {
        string relativePath = Path.Combine("relative", "game");

        string normalized = TrustedPath.NormalizeAbsolutePath(relativePath);

        Assert.Equal(Path.GetFullPath(relativePath), normalized);
    }

    [Fact]
    public void TryNormalizeRelativePath_WhenPathIsSafe_ReturnsNormalizedPath()
    {
        string relativePath = $"Game_Data{Path.AltDirectorySeparatorChar}mod.bin";

        bool result = TrustedPath.TryNormalizeRelativePath(relativePath, out string normalizedPath);

        Assert.True(result);
        Assert.Equal(Path.Combine("Game_Data", "mod.bin"), normalizedPath);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("Game_Data/../mod.bin")]
    [InlineData("Game_Data//mod.bin")]
    [InlineData("Game_Data/mod*.bin")]
    [InlineData("C:/game")]
    [InlineData("/absolute/path")]
    public void TryNormalizeRelativePath_WhenPathIsUnsafe_ReturnsFalse(string path)
    {
        bool result = TrustedPath.TryNormalizeRelativePath(path, out string normalizedPath);

        Assert.False(result);
        Assert.Equal(string.Empty, normalizedPath);
    }

    [Fact]
    public void IsWithinRoot_WhenPathIsInsideRoot_ReturnsTrue()
    {
        string root = Path.Combine(Path.GetTempPath(), "trusted-root");
        string path = Path.Combine(root, "Game_Data", "mod.bin");

        bool result = TrustedPath.IsWithinRoot(path, root);

        Assert.True(result);
    }

    [Fact]
    public void IsWithinRoot_WhenPathEqualsRoot_ReturnsTrue()
    {
        string root = Path.Combine(Path.GetTempPath(), "trusted-root");

        bool result = TrustedPath.IsWithinRoot(root, root);

        Assert.True(result);
    }

    [Fact]
    public void IsWithinRoot_WhenPathSharesRootPrefix_ReturnsFalse()
    {
        string root = Path.Combine(Path.GetTempPath(), "trusted-root");
        string sibling = Path.Combine(Path.GetTempPath(), "trusted-root-other");

        bool result = TrustedPath.IsWithinRoot(sibling, root);

        Assert.False(result);
    }

    [Fact]
    public void IsWithinRoot_WhenPathIsOutsideRoot_ReturnsFalse()
    {
        string root = Path.Combine(Path.GetTempPath(), "trusted-root");
        string outside = Path.Combine(Path.GetTempPath(), "other");

        bool result = TrustedPath.IsWithinRoot(outside, root);

        Assert.False(result);
    }

    [Fact]
    public void PathsEqual_WhenPathsDifferOnlyByCase_FollowsPlatformRules()
    {
        string left = Path.Combine(Path.GetTempPath(), "Game", "mod.bin");
        string right = Path.Combine(Path.GetTempPath(), "game", "MOD.bin");

        bool result = TrustedPath.PathsEqual(left, right);

        Assert.Equal(OperatingSystem.IsWindows(), result);
    }

    [Fact]
    public void FindDuplicatePath_WhenPathsCollideAfterNormalization_ReturnsDuplicate()
    {
        string root = Path.Combine(Path.GetTempPath(), "trusted-root");
        string left = Path.Combine(root, "Game_Data", "mod.bin");
        string right = Path.Combine(root, "Game_Data") + Path.DirectorySeparatorChar + "mod.bin";

        string? duplicate = TrustedPath.FindDuplicatePath([left, right]);

        Assert.Equal(TrustedPath.NormalizeAbsolutePath(left), duplicate);
    }

    [Fact]
    public void FindDuplicatePath_WhenAllPathsAreDistinct_ReturnsNull()
    {
        string root = Path.Combine(Path.GetTempPath(), "trusted-root");
        string first = Path.Combine(root, "a.bin");
        string second = Path.Combine(root, "b.bin");

        string? duplicate = TrustedPath.FindDuplicatePath([first, second]);

        Assert.Null(duplicate);
    }
}

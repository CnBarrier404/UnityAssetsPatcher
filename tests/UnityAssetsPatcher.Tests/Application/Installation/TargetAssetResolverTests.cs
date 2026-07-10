using UnityAssetsPatcher.Application.Installation;
using Xunit;

namespace UnityAssetsPatcher.Tests.Application.Installation;

public sealed class TargetAssetResolverTests
{
    [Fact]
    public void ResolveTargetPaths_ReturnsRegularFileInsideGameDirectory()
    {
        string gameDirectory = Path.Combine(Path.GetTempPath(), $"UnityAssetsPatcher-{Guid.NewGuid():N}");
        string targetDirectory = Path.Combine(gameDirectory, "Game_Data");
        string targetPath = Path.Combine(targetDirectory, "sharedassets0.assets");
        Directory.CreateDirectory(targetDirectory);

        try
        {
            File.WriteAllText(targetPath, "asset");

            Dictionary<string, string> targets = TargetAssetResolver.ResolveTargetPaths(
                gameDirectory,
                ["sharedassets0.assets"]);

            Assert.Equal(Path.GetFullPath(targetPath), targets["sharedassets0.assets"]);
        }
        finally
        {
            Directory.Delete(gameDirectory, true);
        }
    }

    [Fact]
    public void ResolveTargetPaths_DoesNotFollowDirectorySymbolicLinkOutsideGameDirectory()
    {
        string root = Path.Combine(Path.GetTempPath(), $"UnityAssetsPatcher-{Guid.NewGuid():N}");
        string gameDirectory = Path.Combine(root, "game");
        string externalDirectory = Path.Combine(root, "external");
        string linkPath = Path.Combine(gameDirectory, "linked");
        string externalTarget = Path.Combine(externalDirectory, "sharedassets0.assets");
        Directory.CreateDirectory(gameDirectory);
        Directory.CreateDirectory(externalDirectory);

        try
        {
            if (!TryCreateDirectorySymbolicLink(linkPath, externalDirectory, out string? skipReason))
            {
                Assert.Skip(skipReason!);
            }

            File.WriteAllText(externalTarget, "external asset");

            var exception = Assert.Throws<FileNotFoundException>(() =>
                TargetAssetResolver.ResolveTargetPaths(gameDirectory, ["sharedassets0.assets"]));

            Assert.Contains("was not found under game directory", exception.Message);
            Assert.Equal("external asset", File.ReadAllText(externalTarget));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void ResolveTargetPaths_DoesNotAcceptFileSymbolicLinkOutsideGameDirectory()
    {
        string root = Path.Combine(Path.GetTempPath(), $"UnityAssetsPatcher-{Guid.NewGuid():N}");
        string gameDirectory = Path.Combine(root, "game");
        string externalDirectory = Path.Combine(root, "external");
        string linkPath = Path.Combine(gameDirectory, "sharedassets0.assets");
        string externalTarget = Path.Combine(externalDirectory, "victim.assets");
        Directory.CreateDirectory(gameDirectory);
        Directory.CreateDirectory(externalDirectory);

        try
        {
            File.WriteAllText(externalTarget, "external asset");

            if (!TryCreateFileSymbolicLink(linkPath, externalTarget, out string? skipReason))
            {
                Assert.Skip(skipReason!);
            }

            Assert.Throws<FileNotFoundException>(() =>
                TargetAssetResolver.ResolveTargetPaths(gameDirectory, ["sharedassets0.assets"]));
            Assert.Equal("external asset", File.ReadAllText(externalTarget));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    public static bool TryCreateDirectorySymbolicLink(
        string linkPath,
        string targetPath,
        out string? skipReason)
    {
        try
        {
            Directory.CreateSymbolicLink(linkPath, targetPath);
            skipReason = null;
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            skipReason = $"Cannot create directory symbolic link in this environment: {exception.Message}";
            return false;
        }
    }

    public static bool TryCreateFileSymbolicLink(
        string linkPath,
        string targetPath,
        out string? skipReason)
    {
        try
        {
            File.CreateSymbolicLink(linkPath, targetPath);
            skipReason = null;
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            skipReason = $"Cannot create file symbolic link in this environment: {exception.Message}";
            return false;
        }
    }
}

using UnityAssetsPatcher.Application;
using Xunit;

namespace UnityAssetsPatcher.Tests.Application;

public sealed class GameDirectoryResolverTests
{
    [Fact]
    public void ResolveRequired_WhenExplicitDirectoryExists_ReturnsFullPath()
    {
        string gameDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(gameDirectory);

        try
        {
            var resolver = new GameDirectoryResolver([]);

            string result = resolver.ResolveRequired(gameDirectory, null);

            Assert.Equal(Path.GetFullPath(gameDirectory), result);
        }
        finally
        {
            Directory.Delete(gameDirectory, true);
        }
    }

    [Fact]
    public void ResolveRequired_WhenExplicitDirectoryDoesNotExist_ThrowsClearError()
    {
        string gameDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var resolver = new GameDirectoryResolver([]);

        var exception = Assert.Throws<DirectoryNotFoundException>(() =>
            resolver.ResolveRequired(gameDirectory, "Test Game"));

        Assert.Contains("Game directory not found", exception.Message);
        Assert.Contains(Path.GetFullPath(gameDirectory), exception.Message);
    }

    [Fact]
    public void ResolveRequired_WhenDirectoryMissingAndManifestGameMissing_ThrowsClearError()
    {
        var resolver = new GameDirectoryResolver([]);

        var exception = Assert.Throws<DirectoryNotFoundException>(() =>
            resolver.ResolveRequired(null, null));

        Assert.Contains("Game directory was not provided", exception.Message);
        Assert.Contains("manifest does not contain a 'game' property", exception.Message);
    }
}

using UnityAssetsPatcher.Application.Installation;
using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Domain.Integrity;
using Xunit;

namespace UnityAssetsPatcher.Tests.Application.Installation;

public sealed class GameDirectoryResolverTests
{
    [Fact]
    public void ResolveRequired_WhenExplicitDirectoryExists_ReturnsFullPath()
    {
        string gameDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(gameDirectory);

        try
        {
            GameDirectoryResolver resolver = CreateResolver([]);

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
        GameDirectoryResolver resolver = CreateResolver([]);

        var exception = Assert.Throws<DirectoryNotFoundException>(() =>
            resolver.ResolveRequired(gameDirectory, "Test Game"));

        Assert.Contains("Game directory not found", exception.Message);
        Assert.Contains(Path.GetFullPath(gameDirectory), exception.Message);
    }

    [Fact]
    public void ResolveRequired_WhenDirectoryMissingAndManifestGameMissing_ThrowsClearError()
    {
        GameDirectoryResolver resolver = CreateResolver([]);

        var exception = Assert.Throws<DirectoryNotFoundException>(() =>
            resolver.ResolveRequired(null, null));

        Assert.Contains("Game directory was not provided", exception.Message);
        Assert.Contains("manifest does not contain a 'game' property", exception.Message);
    }

    [Fact]
    public void Resolve_WhenLocatorReturnsOneDirectory_ReturnsFullPath()
    {
        string gameDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(gameDirectory);

        try
        {
            GameDirectoryResolver resolver = CreateResolver([gameDirectory]);

            string? result = resolver.Resolve("Test Game");

            Assert.Equal(Path.GetFullPath(gameDirectory), result);
        }
        finally
        {
            Directory.Delete(gameDirectory, true);
        }
    }

    [Fact]
    public void Resolve_WhenLocatorReturnsMultipleDirectories_ReturnsNull()
    {
        string firstDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string secondDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(firstDirectory);
        Directory.CreateDirectory(secondDirectory);

        try
        {
            GameDirectoryResolver resolver = CreateResolver([firstDirectory, secondDirectory]);

            string? result = resolver.Resolve("Test Game");

            Assert.Null(result);
        }
        finally
        {
            Directory.Delete(firstDirectory, true);
            Directory.Delete(secondDirectory, true);
        }
    }

    private static GameDirectoryResolver CreateResolver(IReadOnlyList<string> directories)
    {
        return new GameDirectoryResolver(
            new StubGameInstallationLocator(directories),
            new LocalFileSystemOperations());
    }

    private sealed class StubGameInstallationLocator : IGameInstallationLocator
    {
        private readonly IReadOnlyList<string> _directories;

        public StubGameInstallationLocator(IReadOnlyList<string> directories)
        {
            _directories = directories;
        }

        public IReadOnlyList<string> FindGameDirectories(string game)
        {
            return _directories;
        }
    }

    private sealed class LocalFileSystemOperations : IFileSystemOperations
    {
        public Stream OpenRead(string path)
        {
            throw new NotSupportedException();
        }

        public FileIntegrity ComputeFileIntegrity(string path)
        {
            throw new NotSupportedException();
        }

        public FileAttributes GetAttributes(string path)
        {
            return File.GetAttributes(path);
        }

        public void WriteFileAtomically(string destinationPath, FileDestinationMode mode, Action<Stream> writer)
        {
            throw new NotSupportedException();
        }

        public void CopyFileAtomically(string sourcePath, string destinationPath, FileDestinationMode mode)
        {
            throw new NotSupportedException();
        }

        public void DeleteFile(string path)
        {
            throw new NotSupportedException();
        }

        public void EnsureDirectory(string path)
        {
            throw new NotSupportedException();
        }

        public void MoveDirectory(string sourcePath, string destinationPath)
        {
            throw new NotSupportedException();
        }

        public void DeleteDirectoryTree(string path)
        {
            throw new NotSupportedException();
        }
    }
}

using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Domain.Integrity;
using Xunit;

namespace UnityAssetsPatcher.Application.Tests.IO;

public sealed class TrustedPathResolverTests
{
    [Fact]
    public void ResolveExistingDirectory_WhenDirectoryExists_ReturnsNormalizedPath()
    {
        string path = TrustedPath.NormalizeAbsolutePath(Path.Combine(Path.GetTempPath(), "Game"));
        TrustedPathResolver resolver = CreateResolver((path, FileAttributes.Directory));

        string resolved = resolver.ResolveExistingDirectory(path + Path.DirectorySeparatorChar);

        Assert.Equal(path, resolved);
    }

    [Fact]
    public void ResolveExistingDirectory_WhenDirectoryIsMissing_ThrowsDirectoryNotFoundException()
    {
        string path = TrustedPath.NormalizeAbsolutePath(Path.Combine(Path.GetTempPath(), "missing"));
        TrustedPathResolver resolver = CreateResolver();

        DirectoryNotFoundException exception = Assert.Throws<DirectoryNotFoundException>(
            () => resolver.ResolveExistingDirectory(path));

        Assert.Equal($"The directory does not exist: '{path}'.", exception.Message);
    }

    [Fact]
    public void ResolveExistingDirectory_WhenPathIsFile_ThrowsDirectoryNotFoundException()
    {
        string path = TrustedPath.NormalizeAbsolutePath(Path.Combine(Path.GetTempPath(), "game.bin"));
        TrustedPathResolver resolver = CreateResolver((path, FileAttributes.Normal));

        _ = Assert.Throws<DirectoryNotFoundException>(() => resolver.ResolveExistingDirectory(path));
    }

    [Fact]
    public void ResolveWithinDirectory_WhenRelativePathIsSafe_ReturnsResolvedPath()
    {
        string root = TrustedPath.NormalizeAbsolutePath(Path.Combine(Path.GetTempPath(), "Game"));
        TrustedPathResolver resolver = CreateResolver((root, FileAttributes.Directory));

        string resolved = resolver.ResolveWithinDirectory(root, Path.Combine("Game_Data", "mod.bin"));

        Assert.Equal(Path.Combine(root, "Game_Data", "mod.bin"), resolved);
    }

    [Fact]
    public void ResolveWithinDirectory_WhenRelativePathEscapesRoot_ThrowsIOException()
    {
        string root = TrustedPath.NormalizeAbsolutePath(Path.Combine(Path.GetTempPath(), "Game"));
        TrustedPathResolver resolver = CreateResolver((root, FileAttributes.Directory));

        _ = Assert.Throws<IOException>(
            () => resolver.ResolveWithinDirectory(root, Path.Combine("..", "outside.bin")));
    }

    [Fact]
    public void ResolveWithinDirectory_WhenRelativePathIsRooted_ThrowsIOException()
    {
        string root = TrustedPath.NormalizeAbsolutePath(Path.Combine(Path.GetTempPath(), "Game"));
        string outside = Path.Combine(Path.GetTempPath(), "outside.bin");
        TrustedPathResolver resolver = CreateResolver((root, FileAttributes.Directory));

        _ = Assert.Throws<IOException>(() => resolver.ResolveWithinDirectory(root, outside));
    }

    [Fact]
    public void ResolveWithinDirectory_WhenAncestorIsReparsePoint_ThrowsIOException()
    {
        string root = TrustedPath.NormalizeAbsolutePath(Path.Combine(Path.GetTempPath(), "Game"));
        string linkDirectory = Path.Combine(root, "Game_Data");
        TrustedPathResolver resolver = CreateResolver(
            (root, FileAttributes.Directory),
            (linkDirectory, FileAttributes.Directory | FileAttributes.ReparsePoint));

        _ = Assert.Throws<IOException>(
            () => resolver.ResolveWithinDirectory(root, Path.Combine("Game_Data", "mod.bin")));
    }

    [Fact]
    public void ResolveWithinDirectory_WhenTargetIsReparsePoint_ThrowsIOException()
    {
        string root = TrustedPath.NormalizeAbsolutePath(Path.Combine(Path.GetTempPath(), "Game"));
        string target = Path.Combine(root, "mod.bin");
        TrustedPathResolver resolver = CreateResolver(
            (root, FileAttributes.Directory),
            (target, FileAttributes.Normal | FileAttributes.ReparsePoint));

        _ = Assert.Throws<IOException>(() => resolver.ResolveWithinDirectory(root, "mod.bin"));
    }

    [Fact]
    public void ResolveWithinDirectory_WhenRootItselfIsReparsePoint_AllowsResolution()
    {
        string root = TrustedPath.NormalizeAbsolutePath(Path.Combine(Path.GetTempPath(), "Game"));
        TrustedPathResolver resolver = CreateResolver(
            (root, FileAttributes.Directory | FileAttributes.ReparsePoint));

        string resolved = resolver.ResolveWithinDirectory(root, "mod.bin");

        Assert.Equal(Path.Combine(root, "mod.bin"), resolved);
    }

    private static TrustedPathResolver CreateResolver(params (string Path, FileAttributes Attributes)[] attributes)
    {
        var fileSystem = new StubFileSystemOperations(
            attributes.ToDictionary(
                item => item.Path,
                item => item.Attributes,
                TrustedPath.PathComparer));

        return new TrustedPathResolver(fileSystem);
    }

    private sealed class StubFileSystemOperations : IFileSystemOperations
    {
        private readonly IReadOnlyDictionary<string, FileAttributes> _attributes;

        public StubFileSystemOperations(IReadOnlyDictionary<string, FileAttributes> attributes)
        {
            _attributes = attributes;
        }

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
            if (_attributes.TryGetValue(path, out FileAttributes attributes))
            {
                return attributes;
            }

            throw new FileNotFoundException(null, path);
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

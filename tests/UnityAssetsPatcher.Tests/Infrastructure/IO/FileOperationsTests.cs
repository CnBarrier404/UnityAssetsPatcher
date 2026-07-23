using System.Text;
using UnityAssetsPatcher.Abstractions.IO;
using Xunit;

namespace UnityAssetsPatcher.Tests.Infrastructure.IO;

public sealed class FileOperationsTests
{
    private readonly IFileOperations _operations = TestDependencies.FileOperations;

    [Fact]
    public void Write_WhenDestinationIsMissing_CreatesDestination()
    {
        using TemporaryDirectory temporaryDirectory = new();
        string destinationPath = temporaryDirectory.GetPath("destination.txt");

        _operations.Write(destinationPath, stream => WriteText(stream, "created"));

        Assert.Equal("created", File.ReadAllText(destinationPath));
    }

    [Fact]
    public void Copy_WhenDestinationIsMissing_CopiesSourceAndPreservesIt()
    {
        using TemporaryDirectory temporaryDirectory = new();
        string sourcePath = temporaryDirectory.GetPath("source.txt");
        string destinationPath = temporaryDirectory.GetPath("destination.txt");
        File.WriteAllText(sourcePath, "source");

        _operations.Copy(sourcePath, destinationPath);

        Assert.Equal("source", File.ReadAllText(sourcePath));
        Assert.Equal("source", File.ReadAllText(destinationPath));
    }

    [Fact]
    public void Write_WhenDestinationExists_ReplacesDestination()
    {
        using TemporaryDirectory temporaryDirectory = new();
        string destinationPath = temporaryDirectory.GetPath("destination.txt");
        File.WriteAllText(destinationPath, "old");

        _operations.Write(destinationPath, stream => WriteText(stream, "new"));

        Assert.Equal("new", File.ReadAllText(destinationPath));
    }

    [Fact]
    public void Copy_WhenDestinationExists_CopiesSourceAndReplacesDestination()
    {
        using TemporaryDirectory temporaryDirectory = new();
        string sourcePath = temporaryDirectory.GetPath("source.txt");
        string destinationPath = temporaryDirectory.GetPath("destination.txt");
        File.WriteAllText(sourcePath, "source");
        File.WriteAllText(destinationPath, "old");

        _operations.Copy(sourcePath, destinationPath);

        Assert.Equal("source", File.ReadAllText(sourcePath));
        Assert.Equal("source", File.ReadAllText(destinationPath));
    }

    [Fact]
    public void Write_WhenWriterThrows_CleansTemporaryFileAndPreservesDestination()
    {
        using TemporaryDirectory temporaryDirectory = new();
        string destinationPath = temporaryDirectory.GetPath("destination.txt");
        File.WriteAllText(destinationPath, "old");

        var exception = Assert.Throws<InvalidOperationException>(() => _operations.Write(
            destinationPath,
            stream =>
            {
                stream.WriteByte(1);
                throw new InvalidOperationException("writer failed");
            }));

        Assert.Equal("writer failed", exception.Message);
        Assert.Equal("old", File.ReadAllText(destinationPath));
        Assert.Equal(["destination.txt"], Directory.GetFiles(temporaryDirectory.Path).Select(Path.GetFileName));
    }

    [Fact]
    public void Write_WhenDestinationIsSymbolicLink_ReplacesLinkAndPreservesTarget()
    {
        using TemporaryDirectory temporaryDirectory = new();
        string targetPath = temporaryDirectory.GetPath("target.txt");
        string destinationPath = temporaryDirectory.GetPath("destination.txt");
        File.WriteAllText(targetPath, "target");

        if (!TryCreateFileSymbolicLink(destinationPath, targetPath, out string? skipReason))
        {
            Assert.Skip(skipReason!);
        }

        _operations.Write(
            destinationPath,
            stream => WriteText(stream, "new"));

        Assert.Equal("target", File.ReadAllText(targetPath));
        Assert.Equal("new", File.ReadAllText(destinationPath));
        Assert.False(File.GetAttributes(destinationPath).HasFlag(FileAttributes.ReparsePoint));
    }

    [Fact]
    public void Move_WhenDestinationIsMissing_MovesSource()
    {
        using TemporaryDirectory temporaryDirectory = new();
        string sourcePath = temporaryDirectory.GetPath("source.txt");
        string destinationPath = temporaryDirectory.GetPath("destination.txt");
        File.WriteAllText(sourcePath, "source");

        _operations.Move(sourcePath, destinationPath);

        Assert.False(File.Exists(sourcePath));
        Assert.Equal("source", File.ReadAllText(destinationPath));
    }

    [Fact]
    public void Move_WhenDestinationExists_ReplacesDestination()
    {
        using TemporaryDirectory temporaryDirectory = new();
        string sourcePath = temporaryDirectory.GetPath("source.txt");
        string destinationPath = temporaryDirectory.GetPath("destination.txt");
        File.WriteAllText(sourcePath, "source");
        File.WriteAllText(destinationPath, "old");

        _operations.Move(sourcePath, destinationPath);

        Assert.False(File.Exists(sourcePath));
        Assert.Equal("source", File.ReadAllText(destinationPath));
    }

    [Fact]
    public void Move_WhenSourceIsMissing_PreservesDestination()
    {
        using TemporaryDirectory temporaryDirectory = new();
        string sourcePath = temporaryDirectory.GetPath("missing.txt");
        string destinationPath = temporaryDirectory.GetPath("destination.txt");
        File.WriteAllText(destinationPath, "old");

        Assert.Throws<FileNotFoundException>(() => _operations.Move(sourcePath, destinationPath));

        Assert.Equal("old", File.ReadAllText(destinationPath));
    }

    [Fact]
    public void Move_WhenPathsAreEqual_ThrowsAndPreservesFile()
    {
        using TemporaryDirectory temporaryDirectory = new();
        string path = temporaryDirectory.GetPath("file.txt");
        File.WriteAllText(path, "value");

        Assert.Throws<IOException>(() => _operations.Move(path, path));

        Assert.Equal("value", File.ReadAllText(path));
    }

    [Fact]
    public void Move_WhenDestinationIsInDifferentDirectory_ThrowsAndPreservesSource()
    {
        using TemporaryDirectory sourceDirectory = new();
        using TemporaryDirectory destinationDirectory = new();
        string sourcePath = sourceDirectory.GetPath("source.txt");
        File.WriteAllText(sourcePath, "source");

        Assert.Throws<IOException>(() =>
            _operations.Move(sourcePath, destinationDirectory.GetPath("destination.txt")));

        Assert.Equal("source", File.ReadAllText(sourcePath));
    }

    [Fact]
    public void Delete_WhenFileExists_DeletesFile()
    {
        using TemporaryDirectory temporaryDirectory = new();
        string path = temporaryDirectory.GetPath("file.txt");
        File.WriteAllText(path, "value");

        _operations.Delete(path);

        Assert.False(File.Exists(path));
    }

    [Fact]
    public void Delete_WhenFileIsMissing_Throws()
    {
        using TemporaryDirectory temporaryDirectory = new();
        string path = temporaryDirectory.GetPath("missing.txt");

        var exception = Assert.Throws<FileNotFoundException>(() => _operations.Delete(path));

        Assert.Equal(Path.GetFullPath(path), exception.FileName);
    }

    [Fact]
    public void Delete_WhenPathIsDirectory_ThrowsAndPreservesDirectory()
    {
        using TemporaryDirectory temporaryDirectory = new();
        string path = temporaryDirectory.CreateDirectory("directory");

        Assert.Throws<IOException>(() => _operations.Delete(path));

        Assert.True(Directory.Exists(path));
    }

    [Fact]
    public void Delete_WhenPathIsSymbolicLink_DeletesLinkAndPreservesTarget()
    {
        using TemporaryDirectory temporaryDirectory = new();
        string targetPath = temporaryDirectory.GetPath("target.txt");
        string linkPath = temporaryDirectory.GetPath("link.txt");
        File.WriteAllText(targetPath, "target");

        if (!TryCreateFileSymbolicLink(linkPath, targetPath, out string? skipReason))
        {
            Assert.Skip(skipReason!);
        }

        _operations.Delete(linkPath);

        Assert.False(File.Exists(linkPath));
        Assert.Equal("target", File.ReadAllText(targetPath));
    }

    private static void WriteText(Stream stream, string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        stream.Write(bytes);
    }

    private static bool TryCreateFileSymbolicLink(string linkPath, string targetPath, out string? skipReason)
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

    private sealed class TemporaryDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            Guid.NewGuid().ToString("N"));

        public TemporaryDirectory()
        {
            Directory.CreateDirectory(Path);
        }

        public string CreateDirectory(string name)
        {
            return Directory.CreateDirectory(GetPath(name)).FullName;
        }

        public string GetPath(string name)
        {
            return System.IO.Path.Combine(Path, name);
        }

        public void Dispose()
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}

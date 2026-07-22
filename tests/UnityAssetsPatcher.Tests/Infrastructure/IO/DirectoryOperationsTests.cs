using Microsoft.Extensions.DependencyInjection;
using UnityAssetsPatcher.Infrastructure;
using UnityAssetsPatcher.Infrastructure.IO;
using Xunit;

namespace UnityAssetsPatcher.Tests.Infrastructure.IO;

public sealed class DirectoryOperationsTests
{
    private readonly IDirectoryOperations _operations = TestDependencies.DirectoryOperations;

    [Fact]
    public void Create_WhenPathIsMissing_CreatesDirectoryAndParents()
    {
        using TemporaryDirectory temporaryDirectory = new();
        string path = temporaryDirectory.GetPath(Path.Combine("first", "second"));

        _operations.Create(path);

        Assert.True(Directory.Exists(path));
    }

    [Fact]
    public void Create_WhenPathIsAFile_ThrowsAndPreservesFile()
    {
        using TemporaryDirectory temporaryDirectory = new();
        string path = temporaryDirectory.GetPath("file.txt");
        File.WriteAllText(path, "value");

        Assert.Throws<IOException>(() => _operations.Create(path));

        Assert.Equal("value", File.ReadAllText(path));
    }

    [Fact]
    public void Move_WhenDestinationIsMissing_MovesDirectory()
    {
        using TemporaryDirectory temporaryDirectory = new();
        string sourcePath = temporaryDirectory.CreateDirectory("source");
        string destinationPath = temporaryDirectory.GetPath("destination");
        File.WriteAllText(Path.Combine(sourcePath, "file.txt"), "value");

        _operations.Move(sourcePath, destinationPath);

        Assert.False(Directory.Exists(sourcePath));
        Assert.Equal("value", File.ReadAllText(Path.Combine(destinationPath, "file.txt")));
    }

    [Fact]
    public void Move_WhenDestinationHasDifferentParent_MovesDirectory()
    {
        using TemporaryDirectory temporaryDirectory = new();
        string sourceParentPath = temporaryDirectory.CreateDirectory("source-parent");
        string destinationParentPath = temporaryDirectory.CreateDirectory("destination-parent");
        string sourcePath = Directory.CreateDirectory(Path.Combine(sourceParentPath, "source")).FullName;
        string destinationPath = Path.Combine(destinationParentPath, "destination");
        File.WriteAllText(Path.Combine(sourcePath, "file.txt"), "value");

        _operations.Move(sourcePath, destinationPath);

        Assert.False(Directory.Exists(sourcePath));
        Assert.Equal("value", File.ReadAllText(Path.Combine(destinationPath, "file.txt")));
    }

    [Fact]
    public void Move_WhenSourceAndDestinationAreTheSame_ThrowsArgumentException()
    {
        using TemporaryDirectory temporaryDirectory = new();
        string path = temporaryDirectory.CreateDirectory("directory");

        Assert.Throws<ArgumentException>(() => _operations.Move(path, path));

        Assert.True(Directory.Exists(path));
    }

    [Fact]
    public void Move_WhenDestinationExists_ThrowsAndPreservesSource()
    {
        using TemporaryDirectory temporaryDirectory = new();
        string sourcePath = temporaryDirectory.CreateDirectory("source");
        string destinationPath = temporaryDirectory.CreateDirectory("destination");
        File.WriteAllText(Path.Combine(sourcePath, "source.txt"), "source");
        File.WriteAllText(Path.Combine(destinationPath, "destination.txt"), "destination");

        Assert.Throws<IOException>(() => _operations.Move(sourcePath, destinationPath));

        Assert.Equal("source", File.ReadAllText(Path.Combine(sourcePath, "source.txt")));
        Assert.Equal("destination", File.ReadAllText(Path.Combine(destinationPath, "destination.txt")));
    }

    [Fact]
    public void Move_WhenSourceIsAFile_ThrowsAndPreservesFile()
    {
        using TemporaryDirectory temporaryDirectory = new();
        string sourcePath = temporaryDirectory.GetPath("source.txt");
        string destinationPath = temporaryDirectory.GetPath("destination");
        File.WriteAllText(sourcePath, "source");

        Assert.Throws<IOException>(() => _operations.Move(sourcePath, destinationPath));

        Assert.Equal("source", File.ReadAllText(sourcePath));
        Assert.False(Directory.Exists(destinationPath));
    }

    [Fact]
    public void Move_WhenSourceIsMissing_ThrowsFileNotFoundException()
    {
        using TemporaryDirectory temporaryDirectory = new();
        string sourcePath = temporaryDirectory.GetPath("missing");
        string destinationPath = temporaryDirectory.GetPath("destination");

        Assert.Throws<FileNotFoundException>(() => _operations.Move(sourcePath, destinationPath));

        Assert.False(Directory.Exists(destinationPath));
    }

    [Fact]
    public void Delete_WhenDirectoryExists_RemovesContentsAndDirectory()
    {
        using TemporaryDirectory temporaryDirectory = new();
        string path = temporaryDirectory.CreateDirectory("directory");
        string nestedPath = Directory.CreateDirectory(Path.Combine(path, "nested")).FullName;
        File.WriteAllText(Path.Combine(path, "first.txt"), "first");
        File.WriteAllText(Path.Combine(nestedPath, "second.txt"), "second");

        _operations.Delete(path);

        Assert.False(Directory.Exists(path));
    }

    [Fact]
    public void Delete_WhenPathIsAFile_ThrowsAndPreservesFile()
    {
        using TemporaryDirectory temporaryDirectory = new();
        string path = temporaryDirectory.GetPath("file.txt");
        File.WriteAllText(path, "value");

        Assert.Throws<IOException>(() => _operations.Delete(path));

        Assert.Equal("value", File.ReadAllText(path));
    }

    [Fact]
    public void Delete_WhenPathIsMissing_ThrowsFileNotFoundException()
    {
        using TemporaryDirectory temporaryDirectory = new();
        string path = temporaryDirectory.GetPath("missing");

        Assert.Throws<FileNotFoundException>(() => _operations.Delete(path));
    }

    [Fact]
    public void Delete_WhenDirectoryContainsFileSymbolicLink_ThrowsAndPreservesLinkTarget()
    {
        using TemporaryDirectory temporaryDirectory = new();
        string directoryPath = temporaryDirectory.CreateDirectory("directory");
        string targetPath = temporaryDirectory.GetPath("target.txt");
        string linkPath = Path.Combine(directoryPath, "link.txt");
        string filePath = Path.Combine(directoryPath, "file.txt");
        File.WriteAllText(targetPath, "target");
        File.WriteAllText(filePath, "value");

        if (!TryCreateFileSymbolicLink(linkPath, targetPath, out string? skipReason))
        {
            Assert.Skip(skipReason!);
        }

        Assert.Throws<IOException>(() => _operations.Delete(directoryPath));

        Assert.True(Directory.Exists(directoryPath));
        Assert.Equal("value", File.ReadAllText(filePath));
        Assert.True(File.Exists(linkPath));
        Assert.Equal("target", File.ReadAllText(targetPath));
    }

    [Fact]
    public void AddUnityAssetsPatcherInfrastructure_RegistersDirectoryOperations()
    {
        var services = new ServiceCollection();
        services.AddUnityAssetsPatcherInfrastructure();
        using ServiceProvider serviceProvider = services.BuildServiceProvider();

        var operations = serviceProvider.GetRequiredService<IDirectoryOperations>();

        Assert.IsType<DirectoryOperations>(operations);
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
        private string Path { get; } =
            System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));

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

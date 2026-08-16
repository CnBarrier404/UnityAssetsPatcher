using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Domain.Integrity;
using UnityAssetsPatcher.Infrastructure.IO;
using Xunit;

namespace UnityAssetsPatcher.Infrastructure.Tests.IO;

public sealed class FileSystemOperationsTests
{
    [Fact]
    public void OpenRead_WhenFileExists_ReturnsReadableStream()
    {
        using TemporaryDirectory directory = new();
        string sourcePath = directory.WriteFile("input.txt", "content");
        FileSystemOperations operations = CreateOperations();

        using Stream stream = operations.OpenRead(sourcePath);
        using StreamReader reader = new(stream);

        Assert.Equal("content", reader.ReadToEnd());
    }

    [Fact]
    public void OpenRead_WhenFileDoesNotExist_ThrowsFileNotFoundException()
    {
        using TemporaryDirectory directory = new();
        string sourcePath = directory.GetPath("missing.txt");
        FileSystemOperations operations = CreateOperations();

        var exception = Assert.Throws<FileNotFoundException>(() => operations.OpenRead(sourcePath));

        Assert.Equal(sourcePath, exception.FileName);
    }

    [Fact]
    public void ComputeFileIntegrity_WhenFileContainsKnownContent_ReturnsLengthAndHash()
    {
        using TemporaryDirectory directory = new();
        string sourcePath = directory.WriteFile("input.txt", "abc");
        FileSystemOperations operations = CreateOperations();

        FileIntegrity integrity = operations.ComputeFileIntegrity(sourcePath);

        Assert.Equal(3, integrity.Length);
        Assert.Equal(
            "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad",
            integrity.Sha256);
    }

    [Fact]
    public void ComputeFileIntegrity_WhenFileDoesNotExist_ThrowsFileNotFoundException()
    {
        using TemporaryDirectory directory = new();
        string sourcePath = directory.GetPath("missing.txt");
        FileSystemOperations operations = CreateOperations();

        var exception =
            Assert.Throws<FileNotFoundException>(() => operations.ComputeFileIntegrity(sourcePath));

        Assert.Equal(sourcePath, exception.FileName);
    }

    [Fact]
    public void WriteFileAtomically_WhenModeIsCreateNew_WritesNewFile()
    {
        using TemporaryDirectory directory = new();
        string destinationPath = directory.GetPath("output.bin");

        FileSystemOperations operations = CreateOperations();

        operations.WriteFileAtomically(
            destinationPath,
            FileDestinationMode.CreateNew,
            stream => stream.Write([1, 2, 3]));

        Assert.Equal([1, 2, 3], File.ReadAllBytes(destinationPath));
    }

    [Fact]
    public void WriteFileAtomically_WhenCreateNewDestinationExists_PreservesExistingFile()
    {
        using TemporaryDirectory directory = new();
        string destinationPath = directory.WriteFile("output.txt", "original");

        FileSystemOperations operations = CreateOperations();

        _ = Assert.Throws<IOException>(() =>
            operations.WriteFileAtomically(
                destinationPath,
                FileDestinationMode.CreateNew,
                stream => stream.Write([1, 2, 3])));

        Assert.Equal("original", File.ReadAllText(destinationPath));
    }

    [Fact]
    public void WriteFileAtomically_WhenModeIsReplaceExisting_ReplacesExistingFile()
    {
        using TemporaryDirectory directory = new();
        string destinationPath = directory.WriteFile("output.txt", "old");

        FileSystemOperations operations = CreateOperations();

        operations.WriteFileAtomically(
            destinationPath,
            FileDestinationMode.ReplaceExisting,
            stream =>
            {
                using StreamWriter writer = new(stream, leaveOpen: true);
                writer.Write("new");
            });

        Assert.Equal("new", File.ReadAllText(destinationPath));
    }

    [Fact]
    public void WriteFileAtomically_WhenReplaceExistingDestinationIsMissing_DoesNotCreateFile()
    {
        using TemporaryDirectory directory = new();
        string destinationPath = directory.GetPath("output.txt");

        FileSystemOperations operations = CreateOperations();

        _ = Assert.Throws<FileNotFoundException>(() =>
            operations.WriteFileAtomically(
                destinationPath,
                FileDestinationMode.ReplaceExisting,
                stream => stream.Write([1, 2, 3])));

        Assert.False(File.Exists(destinationPath));
    }

    [Fact]
    public void WriteFileAtomically_WhenModeIsCreateOrReplace_CreatesMissingFile()
    {
        using TemporaryDirectory directory = new();
        string destinationPath = directory.GetPath("output.txt");

        FileSystemOperations operations = CreateOperations();

        operations.WriteFileAtomically(
            destinationPath,
            FileDestinationMode.CreateOrReplace,
            stream => stream.Write([1, 2, 3]));

        Assert.Equal([1, 2, 3], File.ReadAllBytes(destinationPath));
    }

    [Fact]
    public void WriteFileAtomically_WhenWriterFails_PreservesExistingFile()
    {
        using TemporaryDirectory directory = new();
        string destinationPath = directory.WriteFile("output.txt", "original");

        FileSystemOperations operations = CreateOperations();

        _ = Assert.Throws<InvalidOperationException>(() =>
            operations.WriteFileAtomically(
                destinationPath,
                FileDestinationMode.ReplaceExisting,
                _ => throw new InvalidOperationException("write failed")));

        Assert.Equal("original", File.ReadAllText(destinationPath));
    }

    [Fact]
    public void CopyFileAtomically_WhenDestinationExists_ReplacesDestinationAndPreservesSource()
    {
        using TemporaryDirectory directory = new();
        string sourcePath = directory.WriteFile("source.txt", "source");

        string destinationPath = directory.WriteFile("destination.txt", "destination");

        FileSystemOperations operations = CreateOperations();

        operations.CopyFileAtomically(sourcePath, destinationPath, FileDestinationMode.ReplaceExisting);

        Assert.Equal("source", File.ReadAllText(sourcePath));

        Assert.Equal("source", File.ReadAllText(destinationPath));
    }

    [Fact]
    public void DeleteFile_WhenPathIsRegularFile_DeletesFile()
    {
        using TemporaryDirectory directory = new();
        string filePath = directory.WriteFile("content.txt", "content");

        FileSystemOperations operations = CreateOperations();

        operations.DeleteFile(filePath);

        Assert.False(File.Exists(filePath));
    }

    [Fact]
    public void DeleteFile_WhenPathIsDirectory_PreservesDirectory()
    {
        using TemporaryDirectory directory = new();
        string nestedDirectory = directory.CreateDirectory("nested");

        FileSystemOperations operations = CreateOperations();

        _ = Assert.Throws<IOException>(() => operations.DeleteFile(nestedDirectory));

        Assert.True(Directory.Exists(nestedDirectory));
    }

    [Fact]
    public void DirectoryOperations_WhenPathsAreDirectories_MoveAndDeleteTree()
    {
        using TemporaryDirectory directory = new();
        string sourcePath = directory.GetPath("source", "nested");

        string movedPath = directory.GetPath("moved");

        FileSystemOperations operations = CreateOperations();

        operations.EnsureDirectory(sourcePath);

        File.WriteAllText(Path.Combine(sourcePath, "content.txt"), "content");

        operations.MoveDirectory(directory.GetPath("source"), movedPath);

        operations.DeleteDirectoryTree(movedPath);

        Assert.False(Directory.Exists(directory.GetPath("source")));

        Assert.False(Directory.Exists(movedPath));
    }

    [Fact]
    public void DeleteDirectoryTree_WhenTreeContainsLink_DeletesTreeAndPreservesLinkTarget()
    {
        using TemporaryDirectory directory = new();
        using TemporaryDirectory externalDirectory = new();
        string targetPath = externalDirectory.WriteFile("content.txt", "content");

        string deletePath = directory.CreateDirectory("delete");

        CreateDirectoryLink(Path.Combine(deletePath, "external-link"), externalDirectory.Path);

        FileSystemOperations operations = CreateOperations();

        operations.DeleteDirectoryTree(deletePath);

        Assert.False(Directory.Exists(deletePath));

        Assert.Equal("content", File.ReadAllText(targetPath));
    }

    [Fact]
    public void GetAttributes_WhenFileExists_ReturnsFileAttributes()
    {
        using TemporaryDirectory directory = new();
        string filePath = directory.WriteFile("content.txt", "content");
        FileSystemOperations operations = CreateOperations();

        FileAttributes attributes = operations.GetAttributes(filePath);

        Assert.False(attributes.HasFlag(FileAttributes.Directory));
    }

    [Fact]
    public void GetAttributes_WhenDirectoryExists_ReturnsDirectoryAttribute()
    {
        using TemporaryDirectory directory = new();
        string directoryPath = directory.CreateDirectory("nested");
        FileSystemOperations operations = CreateOperations();

        FileAttributes attributes = operations.GetAttributes(directoryPath);

        Assert.True(attributes.HasFlag(FileAttributes.Directory));
    }

    [Fact]
    public void GetAttributes_WhenPathIsMissing_ThrowsFileNotFoundException()
    {
        using TemporaryDirectory directory = new();
        string missingPath = directory.GetPath("missing.txt");
        FileSystemOperations operations = CreateOperations();

        _ = Assert.Throws<FileNotFoundException>(() => operations.GetAttributes(missingPath));
    }

    [Fact]
    public void GetAttributes_WhenPathIsDirectoryJunction_ReportsReparsePoint()
    {
        using TemporaryDirectory directory = new();
        using TemporaryDirectory externalDirectory = new();
        string linkPath = directory.GetPath("external-link");

        CreateDirectoryLink(linkPath, externalDirectory.Path);
        FileSystemOperations operations = CreateOperations();

        FileAttributes attributes = operations.GetAttributes(linkPath);

        Assert.True(attributes.HasFlag(FileAttributes.ReparsePoint));
    }

    private static FileSystemOperations CreateOperations()
    {
        return new FileSystemOperations(NullLogger<FileSystemOperations>.Instance);
    }

    private static void CreateDirectoryLink(string linkPath, string targetPath)
    {
        if (!OperatingSystem.IsWindows())
        {
            Directory.CreateSymbolicLink(linkPath, targetPath);

            return;
        }

        using Process process = Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            UseShellExecute = false,
            CreateNoWindow = true,
            ArgumentList =
            {
                "/c",
                "mklink",
                "/J",
                linkPath,
                targetPath
            }
        }) ?? throw new InvalidOperationException("Failed to start the junction creation process.");

        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Failed to create directory junction: {linkPath}");
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public string Path { get; }

        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"UnityAssetsPatcher-{Guid.NewGuid():N}");

            Directory.CreateDirectory(Path);
        }

        public string CreateDirectory(params string[] segments)
        {
            string path = GetPath(segments);

            Directory.CreateDirectory(path);

            return path;
        }

        public string GetPath(params string[] segments)
        {
            return segments.Aggregate(Path, System.IO.Path.Combine);
        }

        public string WriteFile(string relativePath, string content)
        {
            string path = GetPath(relativePath);

            string? parentDirectory = System.IO.Path.GetDirectoryName(path);

            if (parentDirectory is not null)
            {
                Directory.CreateDirectory(parentDirectory);
            }

            File.WriteAllText(path, content);

            return path;
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                DeleteLinks(Path);

                Directory.Delete(Path, true);
            }
        }

        private static void DeleteLinks(string directoryPath)
        {
            foreach (string entryPath in Directory.EnumerateFileSystemEntries(directoryPath))
            {
                FileAttributes attributes = File.GetAttributes(entryPath);

                if (attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    if (attributes.HasFlag(FileAttributes.Directory))
                    {
                        Directory.Delete(entryPath);
                    }
                    else
                    {
                        File.Delete(entryPath);
                    }

                    continue;
                }

                if (attributes.HasFlag(FileAttributes.Directory))
                {
                    DeleteLinks(entryPath);
                }
            }
        }
    }
}

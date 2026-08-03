using System.IO.Compression;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using UnityAssetsPatcher.Application.Packages;
using UnityAssetsPatcher.Infrastructure.IO;
using UnityAssetsPatcher.Infrastructure.Packages;
using Xunit;

namespace UnityAssetsPatcher.Infrastructure.Tests.Packages;

public sealed class ModPackageArchiveFactoryTests
{
    [Fact]
    public void OpenRead_WhenArchiveIsValid_ExposesMetadataAndOpensEntryByIdentity()
    {
        using TemporaryDirectory directory = new();
        string packagePath = directory.WriteArchive(
            ("Mod/MANIFEST.JSON", "{}"u8.ToArray()),
            ("payload/file.bin", [1, 2, 3]));
        ModPackageArchiveFactory factory = CreateFactory();

        using IModPackageArchive archive = factory.OpenRead(packagePath);

        Assert.Equal(Path.GetFullPath(packagePath), archive.PackagePath);
        Assert.Equal(2, archive.Entries.Count);
        PackageEntryInfo manifest = Assert.Single(
            archive.Entries,
            entry => entry.Path == "Mod/MANIFEST.JSON");
        Assert.Equal(2, manifest.Length);

        using Stream input = archive.OpenEntry(manifest.Id);
        using MemoryStream output = new();

        input.CopyTo(output);

        Assert.Equal("{}"u8.ToArray(), output.ToArray());
    }

    [Fact]
    public void OpenEntry_WhenEntryIdIsOutsideArchive_ThrowsArgumentOutOfRangeException()
    {
        using TemporaryDirectory directory = new();
        string packagePath = directory.WriteArchive(("manifest.json", "{}"u8.ToArray()));
        ModPackageArchiveFactory factory = CreateFactory();

        using IModPackageArchive archive = factory.OpenRead(packagePath);

        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            archive.OpenEntry(new PackageEntryId(archive.Entries.Count)));

        Assert.Equal("entryId", exception.ParamName);
    }

    [Fact]
    public void OpenRead_WhenFileDoesNotExist_ThrowsStandardExceptionAndLogsDebugDiagnostic()
    {
        using TemporaryDirectory directory = new();
        string packagePath = directory.GetPath("missing.zip");
        var fileLogger = new RecordingLogger<FileSystemOperations>();
        var fileSystemOperations = new FileSystemOperations(fileLogger);
        var factory = new ModPackageArchiveFactory(
            fileSystemOperations,
            NullLogger<ModPackageArchiveFactory>.Instance);

        FileNotFoundException exception = Assert.Throws<FileNotFoundException>(() => factory.OpenRead(packagePath));

        Assert.Equal(packagePath, exception.FileName);
        LogEntry failure = Assert.Single(fileLogger.Entries, entry => entry.EventId == 2092);
        Assert.Equal(LogLevel.Debug, failure.Level);
        Assert.Same(exception, failure.Exception);
    }

    [Fact]
    public void OpenRead_WhenFileIsNotAnArchive_ThrowsInvalidDataException()
    {
        using TemporaryDirectory directory = new();
        string packagePath = directory.WriteFile("invalid.zip", "not a zip archive");
        var logger = new RecordingLogger<ModPackageArchiveFactory>();
        ModPackageArchiveFactory factory = CreateFactory(logger);

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => factory.OpenRead(packagePath));

        LogEntry failure = Assert.Single(logger.Entries, entry => entry.EventId == 4090);
        Assert.Same(exception, failure.Exception);
    }

    [Fact]
    public void OpenRead_WhenEntryPathIsUnsafe_LeavesPolicyDecisionToApplication()
    {
        using TemporaryDirectory directory = new();
        string packagePath = directory.WriteArchive(
            ("manifest.json", "{}"u8.ToArray()),
            ("../payload.bin", [1]));
        ModPackageArchiveFactory factory = CreateFactory();

        using IModPackageArchive archive = factory.OpenRead(packagePath);

        Assert.Contains(archive.Entries, entry => entry.Path == "../payload.bin");
    }

    private static ModPackageArchiveFactory CreateFactory(
        ILogger<ModPackageArchiveFactory>? logger = null)
    {
        var fileSystemOperations = new FileSystemOperations(NullLogger<FileSystemOperations>.Instance);

        return new ModPackageArchiveFactory(
            fileSystemOperations,
            logger ?? NullLogger<ModPackageArchiveFactory>.Instance);
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add(new LogEntry(eventId.Id, logLevel, exception));
        }
    }

    private sealed record LogEntry(int EventId, LogLevel Level, Exception? Exception);

    private sealed class TemporaryDirectory : IDisposable
    {
        public string Path { get; }

        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"UnityAssetsPatcher-Packages-{Guid.NewGuid():N}");

            Directory.CreateDirectory(Path);
        }

        public string GetPath(params string[] segments)
        {
            return segments.Aggregate(Path, System.IO.Path.Combine);
        }

        public string WriteFile(string relativePath, string contents)
        {
            string path = GetPath(relativePath);

            File.WriteAllText(path, contents);

            return path;
        }

        public string WriteArchive(params (string Name, byte[] Contents)[] entries)
        {
            string path = GetPath($"{Guid.NewGuid():N}.zip");

            using ZipArchive archive = ZipFile.Open(path, ZipArchiveMode.Create);

            foreach ((string name, byte[] contents) in entries)
            {
                ZipArchiveEntry entry = archive.CreateEntry(name);
                using Stream output = entry.Open();

                output.Write(contents);
            }

            return path;
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}

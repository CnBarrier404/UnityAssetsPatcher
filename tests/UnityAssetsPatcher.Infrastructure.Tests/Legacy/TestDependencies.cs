using UnityAssetsPatcher.Application.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using UnityAssetsPatcher.Application.Backups;
using UnityAssetsPatcher.Application.Installation;
using UnityAssetsPatcher.Infrastructure;
using UnityAssetsPatcher.Infrastructure.IO;
using UnityAssetsPatcher.Infrastructure.Installation;

namespace UnityAssetsPatcher.Tests;

public static class TestDependencies
{
    private static readonly JsonSerializerOptions RecordJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public static IFileSystemOperations FileSystemOperations { get; } =
        new FileSystemOperations(NullLogger<FileSystemOperations>.Instance);

    public static BackupRepository CreateBackupRepository(
        string backupDirectory,
        IFileSystemOperations fileSystemOperations,
        ILogger<BackupRepository>? logger = null)
    {
        var services = new ServiceCollection();

        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(fileSystemOperations);
        services.AddUnityAssetsPatcherBackupRepository(backupDirectory);

        ServiceProvider provider = services.BuildServiceProvider();

        return new BackupRepository(
            provider.GetRequiredService<IBackupRepository>(),
            fileSystemOperations,
            logger);
    }

    public static GameDirectoryResolver CreateGameDirectoryResolver(IEnumerable<string>? steamRoots = null)
    {
        string[] roots = steamRoots?.ToArray() ?? [];
        var options = new SteamInstallationOptions(roots);
        var locator = new SteamGameInstallationLocator(options);

        return new GameDirectoryResolver(locator, FileSystemOperations);
    }

    public static InstallRecord WithRepositoryId(InstallRecord record, string repositoryId)
    {
        return new InstallRecord(
            repositoryId,
            record.GameInstanceFingerprint,
            record.InstallSequence,
            record.Id,
            record.InstalledAt,
            record.ModName,
            record.ModVersion,
            record.ModAuthor,
            record.GameName,
            record.PatchedFiles,
            record.CopiedFiles,
            record.OptionalGroups);
    }

    public static void WriteCommittedRecord(
        BackupRepository repository,
        InstallRecord record,
        string installDirectory)
    {
        BackupRepositoryMetadata metadata = repository.LoadMetadata();
        InstallRecord committedRecord = WithRepositoryId(record, metadata.RepositoryId);

        Directory.CreateDirectory(installDirectory);
        File.WriteAllText(
            Path.Combine(installDirectory, "record.json"),
            JsonSerializer.Serialize(committedRecord, RecordJsonOptions));
    }
}

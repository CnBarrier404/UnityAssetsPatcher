using System.IO.Compression;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using UnityAssetsPatcher.Abstractions.Assets;
using UnityAssetsPatcher.Application;
using UnityAssetsPatcher.Application.Backups;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Installation;
using UnityAssetsPatcher.Domain.Assets;
using UnityAssetsPatcher.Infrastructure;
using UnityAssetsPatcher.Tests;
using UnityAssetsPatcher.Tests.Support;
using Xunit;

namespace UnityAssetsPatcher.Tests.Application.Compatibility;

public sealed class WorkflowServiceCompatibilityTests
{
    [Fact]
    public void InstallPreviewAndApply_WhenPackageContainsPatchAndPayload_PreserveResultsOrderAndTiming()
    {
        using CompatibilityTestDirectory scope = new();
        string targetPath = Path.Combine(scope.GameData, "sharedassets0.assets");
        string payloadPath = Path.Combine(scope.GameData, "mod.bin");
        string packagePath = Path.Combine(scope.Root, "compatibility-mod.zip");
        File.WriteAllText(targetPath, "original");
        WriteInstallPackage(packagePath);
        StubAssetsFileService assetsFileService = CreateAssetsFileService();
        using ServiceProvider provider = CreateProvider(scope.Backup, assetsFileService);
        IWorkflowService workflows = provider.GetRequiredService<IWorkflowService>();
        var request = new InstallRequest(packagePath, scope.Game);

        InstallPreviewResult preview = Success(workflows.PreviewInstall(request));

        Assert.Equal("Test Mod", preview.ModName);
        Assert.Equal("1.0.0", preview.ModVersion);
        Assert.Equal("UnityAssetsPatcher.Tests", preview.ModAuthor);
        Assert.Equal(
            ["read-package", "prepare-sources", "find-game-files", "analyze-changes"],
            preview.Timing.Steps.Select(step => step.Name));
        Assert.True(preview.Timing.Elapsed >= TimeSpan.Zero);
        (string Name, string? Description) optionalGroup = Assert.Single(preview.OptionalGroups);
        Assert.Equal("Bonus payload", optionalGroup.Name);
        Assert.Equal("Optional compatibility content", optionalGroup.Description);
        Assert.Collection(
            preview.Changes,
            change =>
            {
                Assert.Equal(InstallChangeKind.Patch, change.Kind);
                Assert.Equal("sharedassets0.assets", change.Name);
                Assert.Equal(targetPath, change.Path);
                Assert.NotNull(change.Preview);
                Assert.Null(change.BackupPath);
                Assert.Equal(0, change.AssetCount);
                Assert.Equal(0, change.OperationCount);
            },
            change =>
            {
                Assert.Equal(InstallChangeKind.Payload, change.Kind);
                Assert.Equal("payload/mod.bin", change.Name);
                Assert.Equal(payloadPath, change.Path);
                Assert.Null(change.Preview);
            });
        Assert.Equal("original", File.ReadAllText(targetPath));
        Assert.False(File.Exists(payloadPath));

        InstallModResult result = Success(workflows.Install(request));

        Assert.Equal(32, result.InstallId.Length);
        Assert.Equal("Test Mod", result.ModName);
        Assert.Equal("1.0.0", result.ModVersion);
        Assert.Empty(result.OptionalGroups);
        Assert.Equal(BackupRepositoryStatus.Clean, result.Recovery.Status);
        Assert.Equal(
            ["read-package", "prepare-sources", "find-game-files", "analyze-changes", "prepare-patch"],
            result.Timing.Steps.Select(step => step.Name));
        Assert.True(result.Timing.Elapsed >= TimeSpan.Zero);
        Assert.Collection(
            result.Changes,
            change =>
            {
                Assert.Equal(InstallChangeKind.Patch, change.Kind);
                Assert.Equal("sharedassets0.assets", change.Name);
                Assert.Equal(targetPath, change.Path);
                Assert.Null(change.Preview);
                Assert.StartsWith(scope.Backup, change.BackupPath, StringComparison.OrdinalIgnoreCase);
                Assert.Equal(1, change.AssetCount);
                Assert.Equal(1, change.OperationCount);
            },
            change =>
            {
                Assert.Equal(InstallChangeKind.Payload, change.Kind);
                Assert.Equal("payload/mod.bin", change.Name);
                Assert.Equal(payloadPath, change.Path);
                Assert.Null(change.Preview);
                Assert.Null(change.BackupPath);
                Assert.Equal(0, change.AssetCount);
                Assert.Equal(0, change.OperationCount);
            });
        Assert.Equal("patched", File.ReadAllText(targetPath));
        Assert.Equal("payload", File.ReadAllText(payloadPath));
    }

    [Fact]
    public void PreviewUninstall_WhenFilesAndInstallLayersConflict_PreservesBlockersIntegrityAndCanUninstall()
    {
        using CompatibilityTestDirectory scope = new();
        string targetPath = Path.Combine(scope.GameData, "sharedassets0.assets");
        string payloadPath = Path.Combine(scope.GameData, "mod.bin");
        File.WriteAllText(targetPath, "installed-assets");
        File.WriteAllText(payloadPath, "modified");
        var repository = new BackupRepository(
            scope.Backup,
            TestDependencies.FileSystemOperations);
        BackupRepositoryMetadata metadata = repository.LoadMetadata();
        string fingerprint = GameInstanceIdentity.CreateFingerprint(TestDependencies.FileSystemOperations, scope.Game);
        DateTimeOffset targetInstalledAt = DateTimeOffset.Parse("2025-06-15T12:34:56+00:00");
        DateTimeOffset blockerInstalledAt = DateTimeOffset.Parse("2025-06-16T12:34:56+00:00");
        InstallRecord target = CreateInstallRecord(
            metadata.RepositoryId,
            fingerprint,
            1,
            "target-install-v1",
            "Target Mod",
            targetInstalledAt,
            includePayload: true);
        InstallRecord blocker = CreateInstallRecord(
            metadata.RepositoryId,
            fingerprint,
            2,
            "blocking-install-v1",
            "Blocking Mod",
            blockerInstalledAt,
            includePayload: false);
        repository.WriteRecord(target, repository.GetInstallDirectory(target.Id));
        repository.WriteRecord(blocker, repository.GetInstallDirectory(blocker.Id));
        using ServiceProvider provider = CreateProvider(scope.Backup, new StubAssetsFileService([]));
        IWorkflowService workflows = provider.GetRequiredService<IWorkflowService>();

        UninstallPreviewResult preview = Success(workflows.PreviewUninstall(
            new UninstallPreviewRequest(target.Id, scope.Game)));

        Assert.Equal(target.Id, preview.InstallId);
        Assert.Equal("Target Mod", preview.ModName);
        Assert.Equal("1.0.0", preview.ModVersion);
        Assert.Equal(targetInstalledAt, preview.InstalledAt);
        Assert.Equal(
            TestDependencies.FileSystemOperations.ResolveExistingDirectory(scope.Game),
            preview.GameDirectory,
            StringComparer.OrdinalIgnoreCase);
        Assert.False(preview.CanUninstall);
        UninstallBlockingModResult blockingMod = Assert.Single(preview.BlockingMods);
        Assert.Equal("Blocking Mod", blockingMod.ModName);
        Assert.Equal("1.0.0", blockingMod.ModVersion);
        Assert.Equal(blockerInstalledAt, blockingMod.InstalledAt);
        Assert.Equal([Path.Combine("Game_Data", "sharedassets0.assets")], blockingMod.OverlappingAssetsFiles);
        UninstallPreviewRestoredFileResult restoredFile = Assert.Single(preview.RestoredFiles);
        Assert.Equal("sharedassets0.assets", restoredFile.Target);
        Assert.Equal(FileIntegrityStatus.Matches, restoredFile.TargetStatus);
        Assert.Equal(FileIntegrityStatus.Missing, restoredFile.BackupStatus);
        UninstallPreviewDeletedFileResult deletedFile = Assert.Single(preview.DeletedFiles);
        Assert.Equal(payloadPath, deletedFile.DestinationPath);
        Assert.Equal(FileIntegrityStatus.Modified, deletedFile.Status);
    }

    [Fact]
    public void RecoveryMethods_WhenRepositoryStateChanges_PreserveStatusMapping()
    {
        using CompatibilityTestDirectory scope = new();
        CompatibilityFixture.InitializeRepository(scope.Backup);
        using ServiceProvider provider = CreateProvider(scope.Backup, new StubAssetsFileService([]));
        IWorkflowService workflows = provider.GetRequiredService<IWorkflowService>();

        BackupRecoveryReport clean = Success(workflows.CheckPendingTransactions());

        Assert.Equal(BackupRepositoryStatus.Clean, clean.Status);

        string fingerprint = GameInstanceIdentity.CreateFingerprint(TestDependencies.FileSystemOperations, scope.Game);
        string transactionDirectory = CompatibilityFixture.CopyTransaction(
            scope.Backup,
            "install-transaction-uncommitted-v1.json",
            fingerprint);
        File.WriteAllText(Path.Combine(scope.GameData, "data.assets"), "modified");
        File.WriteAllText(Path.Combine(scope.GameData, "mod.bin"), "payload");
        string rollbackDirectory = Path.Combine(transactionDirectory, "rollback");
        Directory.CreateDirectory(rollbackDirectory);
        File.WriteAllText(Path.Combine(rollbackDirectory, "data.assets"), "original");

        BackupRecoveryReport required = Success(workflows.CheckPendingTransactions());
        BackupRecoveryPreview preview = Success(workflows.PreviewPendingTransaction(scope.Game));

        Assert.Equal(BackupRepositoryStatus.RecoveryRequired, required.Status);
        Assert.Equal(BackupRepositoryStatus.RecoveryRequired, preview.Status);

        BackupRecoveryReport recovered = Success(workflows.RecoverPendingTransactions(scope.Game));

        Assert.Equal(BackupRepositoryStatus.Recovered, recovered.Status);

        Directory.CreateDirectory(transactionDirectory);
        File.WriteAllText(Path.Combine(transactionDirectory, BackupTransactionStore.FileName), "not-json");

        BackupRecoveryReport locked = Success(workflows.CheckPendingTransactions());

        Assert.Equal(BackupRepositoryStatus.Locked, locked.Status);
        Assert.Equal(BackupRecoveryIssueCode.RepositoryUnsafe, Assert.Single(locked.Issues).Code);
    }

    [Fact]
    public void BackupRecoveryException_WhenConstructed_PreservesReportAndInnerException()
    {
        var report = new BackupRecoveryReport(
            BackupRepositoryStatus.Locked,
            [],
            [new BackupRecoveryIssue(BackupRecoveryIssueCode.RepositoryUnsafe, "backup/.temp")]);
        var innerException = new IOException("Underlying file failure.");

        var exception = new BackupRecoveryException(
            "Recovery could not complete.",
            report,
            innerException);

        Assert.Equal("Recovery could not complete.", exception.Message);
        Assert.Same(report, exception.Recovery);
        Assert.Same(innerException, exception.InnerException);
    }

    private static ServiceProvider CreateProvider(
        string backupDirectory,
        StubAssetsFileService assetsFileService)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAssetsAccessScopeFactory>(assetsFileService);
        services.AddUnityAssetsPatcherInfrastructure();
        services.AddUnityAssetsPatcherApplication(backupDirectory);

        return services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    private static T Success<T>(OperationResult<T> result)
    {
        return Assert.IsType<OperationSucceeded<T>>(result).Value;
    }

    private static StubAssetsFileService CreateAssetsFileService()
    {
        var fieldTree = new AssetField("Camera", "Camera", null,
        [
            new AssetField("field of view", "float", new AssetFieldValue.Float(90f), []),
            new AssetField("m_CullingMask", "BitField", null,
            [
                new AssetField("m_Bits", "UInt32", new AssetFieldValue.UInt64(3211820983), []),
            ]),
        ]);

        return new StubAssetsFileService(
            [new AssetInfo(4, "Camera")],
            new Dictionary<long, AssetField> { [4] = fieldTree });
    }

    private static void WriteInstallPackage(string packagePath)
    {
        string manifest = TestManifest.CreateJson(
            """
            {
              "copyFiles": [
                { "source": "payload/mod.bin" }
              ],
              "targets": [
                {
                  "file": "sharedassets0.assets",
                  "patches": [
                    {
                      "type": "Camera",
                      "match": { "field of view": 90.0 },
                      "set": {
                        "m_CullingMask.m_Bits": { "from": 3211820983, "to": 931037111 }
                      }
                    }
                  ]
                }
              ],
              "optional": [
                {
                  "name": "Bonus payload",
                  "description": "Optional compatibility content",
                  "copyFiles": [
                    { "source": "payload/bonus.bin" }
                  ]
                }
              ]
            }
            """);

        using ZipArchive archive = ZipFile.Open(packagePath, ZipArchiveMode.Create);
        WriteArchiveEntry(archive, "Mod/manifest.json", manifest);
        WriteArchiveEntry(archive, "payload/mod.bin", "payload");
    }

    private static void WriteArchiveEntry(ZipArchive archive, string entryName, string contents)
    {
        ZipArchiveEntry entry = archive.CreateEntry(entryName);
        using StreamWriter writer = new(entry.Open());
        writer.Write(contents);
    }

    private static InstallRecord CreateInstallRecord(
        string repositoryId,
        string fingerprint,
        long sequence,
        string id,
        string modName,
        DateTimeOffset installedAt,
        bool includePayload)
    {
        IReadOnlyList<InstallRecordCopiedFile> copiedFiles = includePayload
            ?
            [
                new InstallRecordCopiedFile(
                    "payload/mod.bin",
                    Path.Combine("Game_Data", "mod.bin"),
                    FileIntegrity.Create(Encoding.UTF8.GetBytes("payload"))),
            ]
            : [];

        return new InstallRecord(
            repositoryId,
            fingerprint,
            sequence,
            id,
            installedAt,
            modName,
            "1.0.0",
            "Compatibility Author",
            null,
            [
                new InstallRecordPatchedFile(
                    "sharedassets0.assets",
                    Path.Combine("Game_Data", "sharedassets0.assets"),
                    Path.Combine("original", "sharedassets0.assets"),
                    1,
                    1,
                    FileIntegrity.Create(Encoding.UTF8.GetBytes("installed-assets")),
                    FileIntegrity.Create(Encoding.UTF8.GetBytes("backup-assets"))),
            ],
            copiedFiles);
    }
}

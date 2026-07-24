using UnityAssetsPatcher.Application.Backups;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Installation;
using UnityAssetsPatcher.Application.Uninstallation;
using UnityAssetsPatcher.Application.Workflows;
using UnityAssetsPatcher.Tests;
using Xunit;

namespace UnityAssetsPatcher.Tests.Application.Backups;

public sealed class InstallLayerSafetyTests
{
    [Fact]
    public void GameInstanceIdentity_NormalizesEquivalentPathsAndProducesSha256Fingerprint()
    {
        string directory = CreateDirectory();
        try
        {
            string fingerprint = GameInstanceIdentity.CreateFingerprint(
                TestDependencies.FileSystemOperations,
                Path.Combine(directory, ".", "child", ".."));

            Assert.Equal(
                GameInstanceIdentity.CreateFingerprint(TestDependencies.FileSystemOperations, directory),
                fingerprint);
            Assert.Matches("^[0-9a-f]{64}$", fingerprint);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void InstallSequenceAllocator_AllocatesPerGameAndRejectsDuplicateSequence()
    {
        InstallRecord first = Record("a", "game-a", 1, "a.assets");
        InstallRecord otherGame = Record("b", "game-b", 9, "a.assets");

        Assert.Equal(2, InstallSequenceAllocator.Allocate([first, otherGame], "game-a"));
        Assert.Throws<InvalidOperationException>(() =>
            InstallSequenceAllocator.Allocate([first, first with { Id = "duplicate" }], "game-a"));
    }

    [Theory]
    [InlineData("other-repository", "fingerprint", 1)]
    [InlineData("repository", "", 1)]
    [InlineData("repository", "fingerprint", 0)]
    public void InstallRecordValidator_RejectsWrongRepositoryOrInvalidIdentity(
        string repositoryId,
        string fingerprint,
        long sequence)
    {
        InstallRecord record = Record("a", fingerprint, sequence, "a.assets") with
        {
            RepositoryId = repositoryId,
        };

        Assert.ThrowsAny<Exception>(() => InstallRecordValidator.Validate(record, "repository"));
    }

    [Theory]
    [InlineData(-1, "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855")]
    [InlineData(0, "E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855")]
    [InlineData(0, "not-a-sha256")]
    public void InstallRecordValidator_RejectsInvalidFileIntegrity(long length, string sha256)
    {
        InstallRecord record = Record("a", "game", 1, "a.assets");
        InstallRecordPatchedFile file = Assert.Single(record.PatchedFiles);
        record = record with
        {
            PatchedFiles = [file with { InstalledFile = new FileIntegrity(length, sha256) }],
        };

        Assert.Throws<InvalidOperationException>(() => InstallRecordValidator.Validate(record));
    }

    [Fact]
    public void InstallLayerAnalyzer_ReturnsAllLaterOverlappingModsInReverseInstallOrder()
    {
        InstallRecord target = Record("a", "game", 1, "a.assets", "b.assets");
        InstallRecord unrelated = Record("unrelated", "game", 4, "c.assets");
        InstallRecord middle = Record("middle", "game", 2, "b.assets");
        InstallRecord latest = Record("latest", "game", 3, "a.assets", "b.assets");
        InstallRecord otherGame = Record("other", "other-game", 10, "a.assets");

        IReadOnlyList<BlockingInstallRecord> blockers = InstallLayerAnalyzer.FindBlockingRecords(
            target,
            [
                new("target", target),
                new("unrelated", unrelated),
                new("middle", middle),
                new("latest", latest),
                new("other", otherGame),
            ]);

        Assert.Equal(["latest", "middle"], blockers.Select(item => item.Record.Id));
        Assert.Equal(["a.assets", "b.assets"], blockers[0].OverlappingAssetsFiles);
        Assert.Equal(["b.assets"], blockers[1].OverlappingAssetsFiles);
    }

    [Fact]
    public void InstallLayerAnalyzer_WhenLaterRecordHasSameId_StillBlocksUninstall()
    {
        InstallRecord target = Record("shared-id", "game", 1, "a.assets");
        InstallRecord later = Record("shared-id", "game", 2, "a.assets");

        BlockingInstallRecord blocker = Assert.Single(InstallLayerAnalyzer.FindBlockingRecords(
            target,
            [new("target", target), new("later", later)]));

        Assert.Equal(2, blocker.Record.InstallSequence);
        Assert.Equal(["a.assets"], blocker.OverlappingAssetsFiles);
    }

    [Fact]
    public void BackupOperationLock_RejectsConcurrentOwner()
    {
        string directory = CreateDirectory();
        try
        {
            var store = new BackupRepository(
                directory,
                TestDependencies.FileSystemOperations);
            using BackupOperationLock owner = store.AcquireLock();
            Assert.Throws<InvalidOperationException>(() => store.AcquireLock());
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void Uninstall_WhenLaterModOverlaps_BlocksPreviewAndDirectExecutionWithoutMutation()
    {
        using Scenario scenario = Scenario.Create();
        UninstallModWorkflow workflow = scenario.CreateWorkflow();

        UninstallPreviewResult preview = workflow.Preview(
            new UninstallPreviewRequest(scenario.FirstInstallId, scenario.GameDirectory));

        Assert.False(preview.CanUninstall);
        UninstallBlockingModResult blocker = Assert.Single(preview.BlockingMods);
        Assert.Equal("Second Mod", blocker.ModName);
        Assert.Equal([Path.Combine("Game_Data", "sharedassets0.assets")], blocker.OverlappingAssetsFiles);
        Assert.Throws<InvalidOperationException>(() => workflow.Uninstall(
            new UninstallModRequest(scenario.FirstInstallId, scenario.GameDirectory)));
        Assert.Equal("second", File.ReadAllText(scenario.AssetsPath));
        Assert.True(File.Exists(scenario.FirstPayloadPath));
        Assert.True(File.Exists(Path.Combine(scenario.FirstInstallDirectory, "record.json")));
    }

    [Fact]
    public void Uninstall_WhenOverlappingModsAreRemovedInReverseOrder_RestoresEachLayer()
    {
        using Scenario scenario = Scenario.Create();
        UninstallModWorkflow workflow = scenario.CreateWorkflow();

        workflow.Uninstall(new UninstallModRequest(scenario.SecondInstallId, scenario.GameDirectory));
        Assert.Equal("first", File.ReadAllText(scenario.AssetsPath));

        workflow.Uninstall(new UninstallModRequest(scenario.FirstInstallId, scenario.GameDirectory));
        Assert.Equal("original", File.ReadAllText(scenario.AssetsPath));
    }

    [Fact]
    public void Uninstall_WhenGameDirectoryFingerprintDoesNotMatch_RejectsPreview()
    {
        using Scenario scenario = Scenario.Create();
        string otherGame = CreateDirectory();
        try
        {
            Assert.Throws<InvalidOperationException>(() => scenario.CreateWorkflow().Preview(
                new UninstallPreviewRequest(scenario.FirstInstallId, otherGame)));
        }
        finally
        {
            Directory.Delete(otherGame, true);
        }
    }

    private static InstallRecord Record(string id, string fingerprint, long sequence, params string[] files)
    {
        return new InstallRecord(
            string.Empty,
            fingerprint,
            sequence,
            id,
            DateTimeOffset.UnixEpoch.AddMinutes(sequence),
            id,
            "1.0",
            "tests",
            "Game",
            files.Select(file => new InstallRecordPatchedFile(
                Path.GetFileName(file), file, Path.Combine("assets", Path.GetFileName(file)), 1, 1,
                EmptyIntegrity, EmptyIntegrity)).ToArray(),
            []);
    }

    private static string CreateDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class Scenario : IDisposable
    {
        public required string Root { get; init; }
        public required string GameDirectory { get; init; }
        public required string AssetsPath { get; init; }
        public required string FirstPayloadPath { get; init; }
        public required string FirstInstallDirectory { get; init; }
        public required string SecondInstallDirectory { get; init; }
        public required string FirstInstallId { get; init; }
        public required string SecondInstallId { get; init; }
        public required BackupRepository Store { get; init; }

        public static Scenario Create()
        {
            string root = CreateDirectory();
            string game = Path.Combine(root, "game");
            string gameData = Path.Combine(game, "Game_Data");
            string backup = Path.Combine(root, "backup");
            Directory.CreateDirectory(gameData);
            Directory.CreateDirectory(backup);
            string firstAssets = Path.Combine(gameData, "sharedassets0.assets");
            File.WriteAllText(firstAssets, "second");
            string payload = Path.Combine(gameData, "first.payload");
            File.WriteAllText(payload, "payload");

            var store = new BackupRepository(
                backup,
                TestDependencies.FileSystemOperations);
            string repositoryId = store.LoadMetadata().RepositoryId;
            string firstId = Guid.NewGuid().ToString("N");
            string secondId = Guid.NewGuid().ToString("N");
            string firstDirectory = store.GetInstallDirectory(firstId);
            string secondDirectory = store.GetInstallDirectory(secondId);
            string firstBackup = Path.Combine(firstDirectory, "assets", "sharedassets0.assets");
            string secondBackup = Path.Combine(secondDirectory, "assets", Path.GetFileName(firstAssets));
            Directory.CreateDirectory(Path.GetDirectoryName(firstBackup)!);
            Directory.CreateDirectory(Path.GetDirectoryName(secondBackup)!);
            File.WriteAllText(firstBackup, "original");
            File.WriteAllText(secondBackup, "first");
            string fingerprint = GameInstanceIdentity.CreateFingerprint(TestDependencies.FileSystemOperations, game);

            store.WriteRecord(CreateScenarioRecord(
                repositoryId, firstId, "First Mod", 1, fingerprint, game, firstDirectory, firstAssets, firstBackup,
                TextIntegrity("first"),
                [
                    new InstallRecordCopiedFile("first.payload", Path.GetRelativePath(game, payload),
                        FileIntegrity.Create(payload))
                ]), firstDirectory);
            store.WriteRecord(CreateScenarioRecord(
                    repositoryId, secondId, "Second Mod", 2, fingerprint, game, secondDirectory, firstAssets,
                    secondBackup,
                    TextIntegrity("second"), []),
                secondDirectory);

            return new Scenario
            {
                Root = root,
                GameDirectory = game,
                AssetsPath = firstAssets,
                FirstPayloadPath = payload,
                FirstInstallDirectory = firstDirectory,
                SecondInstallDirectory = secondDirectory,
                FirstInstallId = firstId,
                SecondInstallId = secondId,
                Store = store,
            };
        }

        public UninstallModWorkflow CreateWorkflow() => new(
            new UninstallPlanner(Store, new GameDirectoryResolver([]), TestDependencies.FileSystemOperations),
            new UninstallExecutor(
                Store,
                TestDependencies.FileSystemOperations),
            Store);

        public void Dispose()
        {
            if (Directory.Exists(Root)) Directory.Delete(Root, true);
        }

        private static InstallRecord CreateScenarioRecord(
            string repositoryId,
            string id,
            string name,
            long sequence,
            string fingerprint,
            string gameDirectory,
            string installDirectory,
            string assetsPath,
            string backupPath,
            FileIntegrity installedFile,
            IReadOnlyList<InstallRecordCopiedFile> copiedFiles)
        {
            return new InstallRecord(
                repositoryId,
                fingerprint,
                sequence,
                id,
                DateTimeOffset.UnixEpoch.AddMinutes(sequence),
                name,
                $"{sequence}.0",
                "tests",
                "Game",
                [
                    new InstallRecordPatchedFile(
                        Path.GetFileName(assetsPath),
                        Path.GetRelativePath(gameDirectory, assetsPath),
                        Path.GetRelativePath(installDirectory, backupPath),
                        1,
                        1,
                        installedFile,
                        FileIntegrity.Create(backupPath))
                ],
                copiedFiles);
        }
    }

    private static FileIntegrity EmptyIntegrity { get; } = new(
        0,
        "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855");

    private static FileIntegrity TextIntegrity(string contents) =>
        FileIntegrity.Create(System.Text.Encoding.UTF8.GetBytes(contents));
}

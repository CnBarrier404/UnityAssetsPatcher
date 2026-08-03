using UnityAssetsPatcher.Application.Backups;
using UnityAssetsPatcher.Domain.Integrity;
using Xunit;

namespace UnityAssetsPatcher.Application.Tests.Backups;

public sealed class InstallRecordTests
{
    [Fact]
    public void Constructor_WhenSourceCollectionsChange_PreservesSnapshot()
    {
        var patchedFiles = new List<InstallRecordPatchedFile>
        {
            PatchedFile("Game_Data/a.assets", "backups/a.assets"),
        };
        var copiedFiles = new List<InstallRecordCopiedFile>
        {
            new("payload/a.bin", "Game_Data/a.bin", EmptyIntegrity),
        };
        var optionalGroups = new List<string> { "HD Textures" };

        InstallRecord record = Record(
            "install-1",
            "game",
            1,
            patchedFiles,
            copiedFiles,
            optionalGroups);

        patchedFiles.Clear();
        copiedFiles.Clear();
        optionalGroups.Clear();

        Assert.Single(record.PatchedFiles);
        Assert.Single(record.CopiedFiles);
        Assert.Equal(["HD Textures"], record.OptionalGroups);
    }

    [Fact]
    public void Validate_WhenRecordContainsTraversal_RejectsRecord()
    {
        InstallRecord record = Record(
            "install-1",
            "game",
            1,
            [PatchedFile("../outside.assets", "backups/a.assets")]);

        InvalidDataException exception =
            Assert.Throws<InvalidDataException>(() => InstallRecordValidator.Validate(record));

        Assert.Contains("path is not trusted", exception.Message);
    }

    [Fact]
    public void Validate_WhenRepositoryIdDoesNotMatch_RejectsRecord()
    {
        InstallRecord record = Record("install-1", "game", 1);

        InvalidDataException exception =
            Assert.Throws<InvalidDataException>(() => InstallRecordValidator.Validate(record, "other-repository"));

        Assert.Contains("does not belong", exception.Message);
    }

    [Fact]
    public void ValidateAll_WhenGameSequenceIsDuplicated_RejectsRecords()
    {
        InstallRecord first = Record("install-1", "game", 1);
        InstallRecord second = Record("install-2", "game", 1);

        InvalidDataException exception =
            Assert.Throws<InvalidDataException>(() =>
                InstallRecordValidator.ValidateAll([first, second], RepositoryId));

        Assert.Contains("Duplicate install sequence", exception.Message);
    }

    [Fact]
    public void Allocate_WhenOtherGamesHaveLaterSequences_AllocatesNextSequenceForRequestedGame()
    {
        InstallRecord first = Record("install-1", "game", 2);
        InstallRecord otherGame = Record("install-2", "other-game", 8);

        long sequence = InstallSequenceAllocator.Allocate([first, otherGame], "game", RepositoryId);

        Assert.Equal(3, sequence);
    }

    [Fact]
    public void FindBlockingRecords_WhenLaterRecordsOverlap_ReturnsReverseInstallOrder()
    {
        InstallRecord target = Record(
            "target",
            "game",
            1,
            [
                PatchedFile("Game_Data/a.assets", "backups/a.assets"),
                PatchedFile("Game_Data/b.assets", "backups/b.assets"),
            ]);
        InstallRecord middle = Record(
            "middle",
            "game",
            2,
            [PatchedFile("Game_Data/b.assets", "backups/middle-b.assets")]);
        InstallRecord latest = Record(
            "latest",
            "game",
            3,
            [PatchedFile("Game_Data/a.assets", "backups/latest-a.assets")]);
        InstallRecord unrelated = Record(
            "unrelated",
            "other-game",
            9,
            [PatchedFile("Game_Data/a.assets", "backups/other-a.assets")]);

        IReadOnlyList<BlockingInstallRecord> blockers = InstallLayerAnalyzer.FindBlockingRecords(
            target,
            [
                new("target", target),
                new("middle", middle),
                new("latest", latest),
                new("unrelated", unrelated),
            ]);

        Assert.Equal(["latest", "middle"], blockers.Select(blocker => blocker.Record.Id));
        Assert.Equal([Path.Combine("Game_Data", "a.assets")], blockers[0].OverlappingAssetsFiles);
        Assert.Equal([Path.Combine("Game_Data", "b.assets")], blockers[1].OverlappingAssetsFiles);
    }

    private static InstallRecord Record(
        string id,
        string fingerprint,
        long sequence,
        IEnumerable<InstallRecordPatchedFile?>? patchedFiles = null,
        IEnumerable<InstallRecordCopiedFile?>? copiedFiles = null,
        IEnumerable<string?>? optionalGroups = null)
    {
        return new InstallRecord(
            RepositoryId,
            fingerprint,
            sequence,
            id,
            DateTimeOffset.UnixEpoch.AddMinutes(sequence),
            id,
            "1.0",
            "tests",
            "Game",
            patchedFiles ?? [],
            copiedFiles ?? [],
            optionalGroups);
    }

    private static InstallRecordPatchedFile PatchedFile(string assetsPath, string backupPath)
    {
        return new InstallRecordPatchedFile(
            Path.GetFileName(assetsPath),
            assetsPath,
            backupPath,
            1,
            1,
            EmptyIntegrity,
            EmptyIntegrity);
    }

    private const string RepositoryId = "repository-id";

    private static FileIntegrity EmptyIntegrity { get; } = new(
        0,
        "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855");
}

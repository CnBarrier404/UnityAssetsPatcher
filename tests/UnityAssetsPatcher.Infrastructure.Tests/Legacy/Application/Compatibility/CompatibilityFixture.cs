using UnityAssetsPatcher.Application.Backups;

namespace UnityAssetsPatcher.Tests.Application.Compatibility;

public static class CompatibilityFixture
{
    public const string RepositoryId = "compat-repository-v1";

    private const string PlaceholderFingerprint =
        "0000000000000000000000000000000000000000000000000000000000000000";

    public static void InitializeRepository(string backupDirectory)
    {
        Directory.CreateDirectory(backupDirectory);
        CopyFile("repository-v1.json", Path.Combine(backupDirectory, BackupRepository.RepositoryFileName));
        Directory.CreateDirectory(Path.Combine(backupDirectory, BackupRepository.InstalledDirectoryName));
    }

    public static string CopyInstallRecord(
        string backupDirectory,
        string installId,
        string gameInstanceFingerprint)
    {
        string installDirectory = Path.Combine(
            backupDirectory,
            BackupRepository.InstalledDirectoryName,
            installId);
        Directory.CreateDirectory(installDirectory);
        string json = Read("install-record-v1.json")
            .Replace(PlaceholderFingerprint, gameInstanceFingerprint, StringComparison.Ordinal)
            .Replace("committed-install-v1", installId, StringComparison.Ordinal);
        File.WriteAllText(Path.Combine(installDirectory, "record.json"), json);

        return installDirectory;
    }

    public static string CopyTransaction(
        string backupDirectory,
        string fixtureName,
        string gameInstanceFingerprint)
    {
        string transactionDirectory = Path.Combine(
            backupDirectory,
            BackupRepository.TransactionDirectoryName);
        Directory.CreateDirectory(transactionDirectory);
        string json = Read(fixtureName)
            .Replace(PlaceholderFingerprint, gameInstanceFingerprint, StringComparison.Ordinal);
        File.WriteAllText(
            Path.Combine(transactionDirectory, BackupTransactionStore.FileName),
            json);

        return transactionDirectory;
    }

    public static string Read(string fixtureName)
    {
        string path = Path.Combine(
            AppContext.BaseDirectory,
            "Compatibility",
            "Fixtures",
            fixtureName);

        return File.ReadAllText(path);
    }

    private static void CopyFile(string fixtureName, string destinationPath)
    {
        File.Copy(
            Path.Combine(
                AppContext.BaseDirectory,
                "Compatibility",
                "Fixtures",
                fixtureName),
            destinationPath);
    }
}

public sealed class CompatibilityTestDirectory : IDisposable
{
    public string Root { get; } = Path.Combine(
        Path.GetTempPath(),
        $"UnityAssetsPatcher.Compatibility.{Guid.NewGuid():N}");

    public string Backup { get; }
    public string Game { get; }
    public string GameData { get; }

    public CompatibilityTestDirectory()
    {
        Backup = Path.Combine(Root, "backup");
        Game = Path.Combine(Root, "game");
        GameData = Path.Combine(Game, "Game_Data");
        Directory.CreateDirectory(GameData);
    }

    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, true);
        }
    }
}

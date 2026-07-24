using System.Text.Json;
using UnityAssetsPatcher.Abstractions.IO;
using UnityAssetsPatcher.Application.Contracts;

namespace UnityAssetsPatcher.Application.Backups;

public sealed class BackupRepository
{
    public const int CurrentRepositoryFormatVersion = 1;
    public const string RepositoryFileName = "repository.json";
    public const string InstalledDirectoryName = "installed";
    public const string TransactionDirectoryName = ".temp";

    private const string RecordFileName = "record.json";

    private readonly IFileSystemOperations _fileSystemOperations;

    public string BackupDirectory { get; }
    public string InstalledDirectory => Path.Combine(BackupDirectory, InstalledDirectoryName);
    public string TransactionDirectory => Path.Combine(BackupDirectory, TransactionDirectoryName);

    private string MetadataPath => Path.Combine(BackupDirectory, RepositoryFileName);

    public BackupRepository(
        string backupDirectory,
        IFileSystemOperations fileSystemOperations)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(backupDirectory);
        ArgumentNullException.ThrowIfNull(fileSystemOperations);
        BackupDirectory = Path.GetFullPath(backupDirectory);
        _fileSystemOperations = fileSystemOperations;
    }

    public BackupOperationLock AcquireLock()
    {
        EnsureInitialized();
        return BackupOperationLock.Acquire(MetadataPath);
    }

    public BackupRepositoryMetadata LoadMetadata()
    {
        EnsureInitialized();
        using FileStream stream = File.Open(MetadataPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        BackupRepositoryMetadata metadata = JsonSerializer.Deserialize(
                                                stream, BackupJsonContext.Default.BackupRepositoryMetadata)
                                            ?? throw new InvalidOperationException(
                                                $"Backup repository metadata could not be read: {MetadataPath}");
        if (metadata.FormatVersion != CurrentRepositoryFormatVersion ||
            string.IsNullOrWhiteSpace(metadata.RepositoryId))
            throw new NotSupportedException($"Unsupported backup repository format: {metadata.FormatVersion}.");
        return metadata;
    }

    public string CreateTransactionDirectory()
    {
        if (Directory.Exists(TransactionDirectory))
            throw new InvalidOperationException("The backup repository contains an unfinished transaction.");
        _fileSystemOperations.CreateDirectory(TransactionDirectory);
        return TransactionDirectory;
    }

    public string GetInstallDirectory(string installId)
    {
        ValidateId(installId);
        return Path.Combine(InstalledDirectory, installId);
    }

    public void CommitInstall(string preparedInstallDirectory, string installId)
    {
        string destination = GetInstallDirectory(installId);
        if (Directory.Exists(destination)) throw new IOException($"Install record already exists: {installId}");
        _fileSystemOperations.CreateDirectory(InstalledDirectory);
        _fileSystemOperations.MoveDirectory(preparedInstallDirectory, destination);
    }

    public void WriteRecord(InstallRecord record, string installDirectory)
    {
        ArgumentNullException.ThrowIfNull(record);
        string repositoryId = LoadMetadata().RepositoryId;
        if (string.IsNullOrEmpty(record.RepositoryId)) record = record with { RepositoryId = repositoryId };
        InstallRecordValidator.Validate(record, repositoryId);

        string expectedDirectory = GetInstallDirectory(record.Id);
        string fullInstallDirectory = Path.GetFullPath(installDirectory);
        bool isPreparedInstall = _fileSystemOperations.IsPathWithinDirectory(
            fullInstallDirectory,
            TransactionDirectory);
        if (!isPreparedInstall && !_fileSystemOperations.PathsEqual(fullInstallDirectory, expectedDirectory))
            throw new InvalidOperationException("Install records must be saved under the installed directory.");

        BackupJsonStore.Save(
            _fileSystemOperations,
            Path.Combine(installDirectory, RecordFileName),
            record,
            BackupJsonContext.Default.InstallRecord);
    }

    public InstallRecord ReadRecord(string installDirectory)
    {
        return ReadRecordCore(installDirectory, LoadMetadata().RepositoryId);
    }

    public IReadOnlyList<InstallRecordSummary> ListInstalled() => ListRecords()
        .OrderByDescending(item => item.Record.InstallSequence)
        .Select(item => new InstallRecordSummary(item.Record.Id, item.Record.ModName, item.Record.ModVersion,
            item.Record.GameName, item.Record.InstalledAt))
        .ToArray();

    public IReadOnlyList<InstallRecordEntry> ListRecords()
    {
        string repositoryId = LoadMetadata().RepositoryId;
        if (!Directory.Exists(InstalledDirectory)) return [];

        InstallRecordEntry[] records = Directory.EnumerateDirectories(InstalledDirectory)
            .Select(directory => new InstallRecordEntry(directory, ReadRecordCore(directory, repositoryId)))
            .ToArray();
        if (records.Any(entry => !_fileSystemOperations.PathsEqual(
                entry.InstallDirectory,
                GetInstallDirectory(entry.Record.Id))))
            throw new InvalidOperationException("Installed directory name does not match its install record ID.");
        InstallRecordValidator.ValidateAll(records.Select(entry => entry.Record), repositoryId);
        return records;
    }

    public BackupRecoveryPreview PreviewPendingTransaction(string gameDirectory)
    {
        using BackupOperationLock operationLock = AcquireLock();
        return new BackupRecovery(this, _fileSystemOperations).Preview(gameDirectory);
    }

    public BackupRecoveryReport RecoverPendingTransactions(string gameDirectory)
    {
        using BackupOperationLock operationLock = AcquireLock();
        return new BackupRecovery(this, _fileSystemOperations).Recover(gameDirectory);
    }

    public BackupRecoveryReport CheckPendingTransactions()
    {
        using BackupOperationLock operationLock = AcquireLock();

        return new BackupRecovery(this, _fileSystemOperations).Check();
    }

    public BackupRecoveryReport CheckPendingTransactionsUnderLock() =>
        new BackupRecovery(this, _fileSystemOperations).Check();

    public BackupRecoveryReport RecoverTrustedUnderLock(BackupTransaction transaction, string gameDirectory) =>
        new BackupRecovery(this, _fileSystemOperations).RecoverTrusted(transaction, gameDirectory);

    private void EnsureInitialized()
    {
        _fileSystemOperations.CreateDirectory(BackupDirectory);
        if (File.Exists(MetadataPath)) return;

        var metadata = new BackupRepositoryMetadata(CurrentRepositoryFormatVersion, Guid.NewGuid().ToString("N"));
        BackupJsonStore.Save(
            _fileSystemOperations,
            MetadataPath,
            metadata,
            BackupJsonContext.Default.BackupRepositoryMetadata);
        _fileSystemOperations.CreateDirectory(InstalledDirectory);
    }

    private static InstallRecord ReadRecordCore(string installDirectory, string repositoryId)
    {
        string path = Path.Combine(installDirectory, RecordFileName);
        using FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        InstallRecord record = JsonSerializer.Deserialize(stream, BackupJsonContext.Default.InstallRecord)
                               ?? throw new InvalidOperationException($"Install record could not be read: {path}");
        InstallRecordValidator.Validate(record, repositoryId);
        return record;
    }

    private static void ValidateId(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || id.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            id is "." or ".." || id.Contains(Path.DirectorySeparatorChar) ||
            id.Contains(Path.AltDirectorySeparatorChar))
            throw new InvalidOperationException($"Invalid install ID: {id}");
    }
}

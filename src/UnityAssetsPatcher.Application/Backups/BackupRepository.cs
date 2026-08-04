using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Application.Contracts;

namespace UnityAssetsPatcher.Application.Backups;

public sealed class BackupRepository : IBackupService
{
    public const int CurrentRepositoryFormatVersion = 1;
    public const string RepositoryFileName = "repository.json";
    public const string InstalledDirectoryName = "installed";
    public const string TransactionDirectoryName = ".temp";
    public const string LockFileName = ".lock";

    private readonly IFileSystemOperations _fileSystemOperations;
    private readonly IBackupRepository _repository;
    private readonly ILogger<BackupRepository> _logger;

    public string BackupDirectory => _repository.RepositoryDirectory;
    public string InstalledDirectory => _repository.InstalledDirectory;
    public string TransactionDirectory => _repository.TransactionDirectory;

    public BackupRepository(
        IBackupRepository repository,
        IFileSystemOperations fileSystemOperations,
        ILogger<BackupRepository>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(fileSystemOperations);
        _repository = repository;
        _fileSystemOperations = fileSystemOperations;
        _logger = logger ?? NullLogger<BackupRepository>.Instance;
    }

    public BackupOperationLock AcquireLock()
    {
        _repository.LoadOrCreateMetadata();

        return BackupOperationLock.Acquire(Path.Combine(BackupDirectory, LockFileName));
    }

    public BackupRepositoryMetadata LoadMetadata()
    {
        return _repository.LoadOrCreateMetadata();
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
        return _repository.GetInstallDirectory(installId);
    }

    public void CommitInstall(string preparedInstallDirectory, string installId)
    {
        _repository.CommitInstall(preparedInstallDirectory, installId);
    }

    public void WriteRecord(InstallRecord record, string installDirectory)
    {
        ArgumentNullException.ThrowIfNull(record);
        _repository.WritePreparedRecord(record, installDirectory);
    }

    public InstallRecord ReadRecord(string installDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(installDirectory);
        string installId = Path.GetFileName(Path.TrimEndingDirectorySeparator(installDirectory));

        return _repository.ReadRecord(installId).Record;
    }

    public IReadOnlyList<InstallRecordSummary> ListInstalled() => ListRecords()
        .OrderByDescending(item => item.Record.InstallSequence)
        .Select(item => new InstallRecordSummary(item.Record.Id, item.Record.ModName, item.Record.ModVersion,
            item.Record.GameName, item.Record.InstalledAt))
        .ToArray();

    public IReadOnlyList<InstallRecordEntry> ListRecords()
    {
        return _repository.ListRecords();
    }

    public BackupRecoveryPreview PreviewPendingTransaction(string gameDirectory)
    {
        using BackupOperationLock operationLock = AcquireLock();
        return new BackupRecovery(this, _fileSystemOperations).Preview(gameDirectory);
    }

    public BackupRecoveryReport RecoverPendingTransactions(string gameDirectory)
    {
        _logger.LogInformation("Recovering pending transactions for {GameDirectory}", gameDirectory);
        using BackupOperationLock operationLock = AcquireLock();
        BackupRecoveryReport report = new BackupRecovery(this, _fileSystemOperations).Recover(gameDirectory);
        _logger.LogInformation("Recovery finished with status {RecoveryStatus}", report.Status);
        return report;
    }

    public BackupRecoveryReport CheckPendingTransactions()
    {
        using BackupOperationLock operationLock = AcquireLock();

        return new BackupRecovery(this, _fileSystemOperations).Check();
    }

    public BackupRecoveryReport CheckPendingTransactionsUnderLock() =>
        new BackupRecovery(this, _fileSystemOperations).Check();

    public BackupRecoveryReport RecoverTrustedUnderLock(BackupTransaction transaction, string gameDirectory)
    {
        _logger.LogInformation(
            "Rolling back {OperationKind} transaction for install {InstallId}",
            transaction.Kind,
            transaction.InstallId);
        BackupRecoveryReport report =
            new BackupRecovery(this, _fileSystemOperations).RecoverTrusted(transaction, gameDirectory);
        _logger.LogInformation("Rollback finished with status {RecoveryStatus}", report.Status);
        return report;
    }

    public IReadOnlyList<InstallRecordSummary> ListInstalledMods()
    {
        return ListInstalled();
    }

    public BackupRecoveryReport CheckRecovery()
    {
        return CheckPendingTransactions();
    }

    public BackupRecoveryPreview PreviewRecovery(string gameDirectory)
    {
        return PreviewPendingTransaction(gameDirectory);
    }

    public BackupRecoveryReport Recover(string gameDirectory)
    {
        return RecoverPendingTransactions(gameDirectory);
    }
}

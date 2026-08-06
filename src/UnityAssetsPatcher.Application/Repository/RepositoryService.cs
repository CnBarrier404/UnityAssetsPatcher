using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Application.Contracts;

namespace UnityAssetsPatcher.Application.Repository;

public sealed class RepositoryService : IRepositoryService
{
    public const int CurrentRepositoryFormatVersion = 2;
    public const int LegacyRepositoryFormatVersion = 1;
    public const string RepositoryFileName = "repository.json";
    public const string TransactionDirectoryName = ".temp";
    public const string LockFileName = ".lock";

    private readonly IFileSystemOperations _fileSystemOperations;
    private readonly IRepository _repository;
    private readonly ICompositionRepository _compositionRepository;
    private readonly ILogger<RepositoryService> _logger;

    public string RepositoryDirectory => _repository.RepositoryDirectory;
    public string TransactionDirectory => _repository.TransactionDirectory;

    public RepositoryService(
        IRepository repository,
        ICompositionRepository compositionRepository,
        IFileSystemOperations fileSystemOperations,
        ILogger<RepositoryService>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(compositionRepository);
        ArgumentNullException.ThrowIfNull(fileSystemOperations);
        _repository = repository;
        _compositionRepository = compositionRepository;
        _fileSystemOperations = fileSystemOperations;
        _logger = logger ?? NullLogger<RepositoryService>.Instance;
    }

    public RepositoryOperationLock AcquireLock()
    {
        _repository.LoadOrCreateMetadata();

        return RepositoryOperationLock.Acquire(Path.Combine(RepositoryDirectory, LockFileName));
    }

    public RepositoryMetadata LoadMetadata()
    {
        return _repository.LoadOrCreateMetadata();
    }

    public RepositoryMetadata RequireWritableMetadata()
    {
        RepositoryMetadata metadata = LoadMetadata();

        if (metadata.FormatVersion == LegacyRepositoryFormatVersion)
        {
            throw new LegacyRepositoryWriteException();
        }

        if (metadata.FormatVersion != CurrentRepositoryFormatVersion)
        {
            throw new NotSupportedException($"Unsupported backup repository format: {metadata.FormatVersion}.");
        }

        return metadata;
    }

    public string CreateTransactionDirectory()
    {
        if (Directory.Exists(TransactionDirectory))
            throw new InvalidOperationException("The backup repository contains an unfinished transaction.");
        _fileSystemOperations.CreateDirectory(TransactionDirectory);
        return TransactionDirectory;
    }

    public IReadOnlyList<InstallRecordSummary> ListLegacyInstalled() => _repository.ListLegacyRecords()
        .Select(item => item.Record)
        .OrderByDescending(record => record.InstallSequence)
        .Select(record => new InstallRecordSummary(record.Id, record.ModName, record.ModVersion,
            record.GameName, record.InstalledAt))
        .ToArray();

    public string GetLegacyInstallDirectory(string installId)
    {
        return _repository.GetLegacyInstallDirectory(installId);
    }

    public LegacyInstallRecordEntry ReadLegacyRecord(string installId)
    {
        return _repository.ReadLegacyRecord(installId);
    }

    public IReadOnlyList<LegacyInstallRecordEntry> ListLegacyRecords()
    {
        return _repository.ListLegacyRecords();
    }

    public RepositoryRecoveryPreview PreviewPendingTransaction(string gameDirectory)
    {
        using RepositoryOperationLock operationLock = AcquireLock();
        return new RepositoryRecovery(this, _compositionRepository, _fileSystemOperations).Preview(gameDirectory);
    }

    public RepositoryRecoveryReport RecoverPendingTransactions(string gameDirectory)
    {
        _logger.LogInformation("Recovering pending transactions for {GameDirectory}", gameDirectory);
        using RepositoryOperationLock operationLock = AcquireLock();
        _ = RequireWritableMetadata();
        RepositoryRecoveryReport report =
            new RepositoryRecovery(this, _compositionRepository, _fileSystemOperations).Recover(gameDirectory);
        _logger.LogInformation("Recovery finished with status {RecoveryStatus}", report.Status);
        return report;
    }

    public RepositoryRecoveryReport CheckPendingTransactions()
    {
        using RepositoryOperationLock operationLock = AcquireLock();

        return new RepositoryRecovery(this, _compositionRepository, _fileSystemOperations).Check();
    }

    public RepositoryRecoveryReport CheckPendingTransactionsUnderLock() =>
        new RepositoryRecovery(this, _compositionRepository, _fileSystemOperations).Check();

    public RepositoryRecoveryReport RecoverTrustedUnderLock(RepositoryTransaction transaction, string gameDirectory)
    {
        _logger.LogInformation(
            "Rolling back {OperationKind} transaction for install {InstallId}",
            transaction.Kind,
            transaction.InstallId);
        RepositoryRecoveryReport report =
            new RepositoryRecovery(this, _compositionRepository, _fileSystemOperations)
                .RecoverTrusted(transaction, gameDirectory);
        _logger.LogInformation("Rollback finished with status {RecoveryStatus}", report.Status);
        return report;
    }

    public IReadOnlyList<InstallRecordSummary> ListInstalledMods()
    {
        if (LoadMetadata().FormatVersion == LegacyRepositoryFormatVersion)
        {
            return ListLegacyInstalled();
        }

        return _compositionRepository.Layers
            .ListLayers()
            .Select(entry => new InstallRecordSummary(
                entry.Record.Id,
                entry.Record.ModName,
                entry.Record.ModVersion,
                entry.Record.GameName,
                entry.Record.InstalledAt))
            .ToArray();
    }

    public RepositoryRecoveryReport CheckRecovery()
    {
        return CheckPendingTransactions();
    }

    public RepositoryRecoveryPreview PreviewRecovery(string gameDirectory)
    {
        return PreviewPendingTransaction(gameDirectory);
    }

    public RepositoryRecoveryReport Recover(string gameDirectory)
    {
        return RecoverPendingTransactions(gameDirectory);
    }
}

public sealed class LegacyRepositoryWriteException : NotSupportedException
{
    public LegacyRepositoryWriteException()
        : base(
            "This backup repository uses the legacy format. Uninstall all mods with the previous version and " +
            "try again, or verify that the game files have been restored manually before deleting the repository " +
            "and creating a new one.") { }
}

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Application.Contracts;

namespace UnityAssetsPatcher.Application.Repository;

public sealed class RepositoryService
{
    public const int CurrentRepositoryFormatVersion = 2;
    public const string RepositoryFileName = "repository.json";
    public const string TransactionDirectoryName = ".temp";
    public const string LockFileName = ".lock";

    private readonly IFileSystemOperations _fileSystemOperations;
    private readonly IRepositoryStorage _repository;
    private readonly ICompositionRepository _compositionRepository;
    private readonly ILogger<RepositoryService> _logger;

    public string RepositoryDirectory => _repository.RepositoryDirectory;
    public string TransactionDirectory => _repository.TransactionDirectory;

    public RepositoryService(
        IRepositoryStorage repository,
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

        if (metadata.FormatVersion != CurrentRepositoryFormatVersion)
        {
            throw new NotSupportedException($"Unsupported backup repository format: {metadata.FormatVersion}.");
        }

        return metadata;
    }

    public string CreateTransactionDirectory()
    {
        if (Directory.Exists(TransactionDirectory))
        {
            throw new InvalidOperationException("The backup repository contains an unfinished transaction.");
        }

        _fileSystemOperations.CreateDirectory(TransactionDirectory);
        return TransactionDirectory;
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

    public RepositoryRecoveryReport CheckPendingTransactionsUnderLock()
    {
        return new RepositoryRecovery(this, _compositionRepository, _fileSystemOperations).Check();
    }

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
        _ = LoadMetadata();

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
}

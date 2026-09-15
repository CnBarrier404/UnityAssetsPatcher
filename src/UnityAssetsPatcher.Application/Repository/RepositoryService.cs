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

    private readonly IFileSystemOperations _fileSystemOperations;
    private readonly IRepositoryStore _repositoryStore;
    private readonly IRepositoryOperationLockProvider _operationLockProvider;
    private readonly ILogger<RepositoryService> _logger;

    public string RepositoryDirectory => _repositoryStore.RepositoryDirectory;
    public string TransactionDirectory => _repositoryStore.TransactionDirectory;

    public RepositoryService(
        IRepositoryStore repositoryStore,
        IFileSystemOperations fileSystemOperations,
        IRepositoryOperationLockProvider operationLockProvider,
        ILogger<RepositoryService>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(repositoryStore);
        ArgumentNullException.ThrowIfNull(fileSystemOperations);
        ArgumentNullException.ThrowIfNull(operationLockProvider);
        _repositoryStore = repositoryStore;
        _fileSystemOperations = fileSystemOperations;
        _operationLockProvider = operationLockProvider;
        _logger = logger ?? NullLogger<RepositoryService>.Instance;
    }

    public IRepositoryOperationLock AcquireLock()
    {
        _repositoryStore.LoadOrCreateMetadata();

        return AcquireOperationLock();
    }

    public RepositoryMetadata LoadMetadata()
    {
        return _repositoryStore.LoadOrCreateMetadata();
    }

    public RepositoryMetadata RequireWritableMetadata()
    {
        return LoadMetadata();
    }

    public RepositoryClearResult ClearUnsupportedRepository()
    {
        using IRepositoryOperationLock operationLock = AcquireOperationLock();

        return _repositoryStore.ClearUnsupportedRepository(operationLock);
    }

    private IRepositoryOperationLock AcquireOperationLock()
    {
        try
        {
            return _operationLockProvider.Acquire();
        }
        catch (IOException exception) when (IsLockContention(exception))
        {
            throw new RepositoryOperationLockedException(exception);
        }
    }

    private static bool IsLockContention(IOException exception)
    {
        // FileMode.CreateNew reports Win32 file-exists/sharing errors on Windows,
        // and raw EEXIST (17) on Unix. Other I/O failures are not lock contention.
        return OperatingSystem.IsWindows()
            ? exception.HResult is unchecked((int)0x80070050) or unchecked((int)0x800700B7)
                or unchecked((int)0x80070020) or unchecked((int)0x80070021)
            : exception.HResult == 17;
    }

    public string CreateTransactionDirectory()
    {
        if (Directory.Exists(TransactionDirectory))
        {
            throw new InvalidOperationException("The backup repository contains an unfinished transaction.");
        }

        _fileSystemOperations.EnsureDirectory(TransactionDirectory);
        return TransactionDirectory;
    }

    public RepositoryRecoveryPreview PreviewPendingTransaction(string gameDirectory)
    {
        using IRepositoryOperationLock operationLock = AcquireLock();
        return new RepositoryRecovery(this, _repositoryStore, _fileSystemOperations)
            .Preview(gameDirectory);
    }

    public RepositoryRecoveryReport RecoverPendingTransactions(string gameDirectory)
    {
        _logger.LogInformation("Recovering pending transactions for {GameDirectory}", gameDirectory);
        using IRepositoryOperationLock operationLock = AcquireLock();
        _ = RequireWritableMetadata();
        RepositoryRecoveryReport report =
            new RepositoryRecovery(this, _repositoryStore, _fileSystemOperations)
                .Recover(gameDirectory);
        _logger.LogInformation("Recovery finished with status {RecoveryStatus}", report.Status);
        return report;
    }

    public RepositoryRecoveryReport CheckPendingTransactionsUnderLock()
    {
        return new RepositoryRecovery(this, _repositoryStore, _fileSystemOperations).Check();
    }

    public RepositoryRecoveryReport RecoverTrustedUnderLock(RepositoryTransaction transaction, string gameDirectory)
    {
        _logger.LogInformation(
            "Rolling back {OperationKind} transaction for install {InstallId}",
            transaction.Kind,
            transaction.InstallId);
        RepositoryRecoveryReport report =
            new RepositoryRecovery(this, _repositoryStore, _fileSystemOperations)
                .RecoverTrusted(transaction, gameDirectory);
        _logger.LogInformation("Rollback finished with status {RecoveryStatus}", report.Status);
        return report;
    }

    public IReadOnlyList<InstallRecordSummary> ListInstalledMods()
    {
        _ = LoadMetadata();

        return _repositoryStore.Layers
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

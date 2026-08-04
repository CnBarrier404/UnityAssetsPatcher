using Microsoft.Extensions.Logging;
using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Domain.Integrity;

namespace UnityAssetsPatcher.Infrastructure.Backups;

internal sealed class FileBackupStore
{
    private readonly string _installedDirectory;
    private readonly string _transactionDirectory;
    private readonly IFileSystemOperations _fileSystemOperations;
    private readonly TrustedPathResolver _pathResolver;
    private readonly ILogger<FileBackupStore> _logger;

    public FileBackupStore(
        string repositoryDirectory,
        IFileSystemOperations fileSystemOperations,
        ILogger<FileBackupStore> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryDirectory);
        ArgumentNullException.ThrowIfNull(fileSystemOperations);
        ArgumentNullException.ThrowIfNull(logger);

        string repositoryDirectory1 = TrustedPath.NormalizeAbsolutePath(repositoryDirectory);
        _installedDirectory = Path.Combine(repositoryDirectory1, FileBackupCatalogStore.InstalledDirectoryName);
        _transactionDirectory = Path.Combine(repositoryDirectory1, FileBackupCatalogStore.TransactionDirectoryName);
        _fileSystemOperations = fileSystemOperations;
        _pathResolver = new TrustedPathResolver(fileSystemOperations);
        _logger = logger;
    }

    public FileIntegrity StoreVerifiedCopy(
        string sourcePath,
        string preparedInstallDirectory,
        string backupRelativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(preparedInstallDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(backupRelativePath);

        string source = TrustedPath.NormalizeAbsolutePath(sourcePath);

        EnsureRegularFile(source, "Backup source");

        string preparedDirectory = ResolveRepositoryChild(
            _transactionDirectory,
            preparedInstallDirectory,
            "Prepared install");

        string destination = _pathResolver.ResolveWithinDirectory(preparedDirectory, backupRelativePath);

        if (TrustedPath.PathsEqual(source, destination))
        {
            throw new IOException("Backup source and destination must be different files.");
        }

        string destinationDirectory = Path.GetDirectoryName(destination) ??
                                      throw new IOException($"Cannot resolve backup directory: {destination}");

        _fileSystemOperations.EnsureDirectory(destinationDirectory);

        destination = _pathResolver.ResolveWithinDirectory(preparedDirectory, backupRelativePath);

        FileIntegrity expected = _fileSystemOperations.ComputeFileIntegrity(source);

        _fileSystemOperations.CopyFileAtomically(
            source,
            destination,
            FileDestinationMode.CreateNew);

        FileIntegrity actual = _fileSystemOperations.ComputeFileIntegrity(destination);

        if (!expected.Matches(actual))
        {
            _fileSystemOperations.DeleteFile(destination);

            throw new IOException($"Backup verification failed: {source}");
        }

        BackupRepositoryLog.BackupStored(_logger, source, destination);

        return actual;
    }

    public string ResolveBackupPath(string installDirectory, string backupRelativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(installDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(backupRelativePath);

        string fullInstallDirectory = TrustedPath.NormalizeAbsolutePath(installDirectory);

        if (TrustedPath.IsWithinRoot(fullInstallDirectory, _installedDirectory) &&
            !TrustedPath.PathsEqual(fullInstallDirectory, _installedDirectory) ||
            TrustedPath.IsWithinRoot(fullInstallDirectory, _transactionDirectory) &&
            !TrustedPath.PathsEqual(fullInstallDirectory, _transactionDirectory))
        {
            return ResolveRepositoryChild(fullInstallDirectory, backupRelativePath, "Backup");
        }

        throw new InvalidOperationException("Install directory is outside the backup repository.");
    }

    private string ResolveRepositoryChild(string rootDirectory, string childPath, string description)
    {
        string root = _pathResolver.ResolveExistingDirectory(rootDirectory);
        string fullChildPath = TrustedPath.NormalizeAbsolutePath(childPath);

        if (!Path.IsPathRooted(childPath))
        {
            fullChildPath = Path.GetFullPath(Path.Combine(root, childPath));
        }

        if (TrustedPath.PathsEqual(fullChildPath, root) || !TrustedPath.IsWithinRoot(fullChildPath, root))
        {
            throw new InvalidOperationException($"{description} path is outside the backup repository.");
        }

        string relativePath = Path.GetRelativePath(root, fullChildPath);

        return _pathResolver.ResolveWithinDirectory(root, relativePath);
    }

    private void EnsureRegularFile(string path, string description)
    {
        FileAttributes attributes = _fileSystemOperations.GetAttributes(path);

        if (attributes.HasFlag(FileAttributes.Directory) || attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            throw new IOException($"{description} must be a regular file: {path}");
        }
    }
}

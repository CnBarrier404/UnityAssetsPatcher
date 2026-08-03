using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.Logging;
using UnityAssetsPatcher.Application.Backups;
using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Domain.Integrity;

namespace UnityAssetsPatcher.Infrastructure.Backups;

internal sealed class FileBackupCatalogStore
{
    public const int CurrentRepositoryFormatVersion = 1;
    public const string RepositoryFileName = "repository.json";
    public const string InstalledDirectoryName = "installed";
    public const string TransactionDirectoryName = ".temp";
    public const string RecordFileName = "record.json";

    public string RepositoryDirectory { get; }
    public string InstalledDirectory { get; }
    public string TransactionDirectory { get; }

    private string MetadataPath { get; }

    private readonly IFileSystemOperations _fileSystemOperations;
    private readonly TrustedPathResolver _pathResolver;
    private readonly ILogger<FileBackupCatalogStore> _logger;

    public FileBackupCatalogStore(
        string repositoryDirectory,
        IFileSystemOperations fileSystemOperations,
        ILogger<FileBackupCatalogStore> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryDirectory);
        ArgumentNullException.ThrowIfNull(fileSystemOperations);
        ArgumentNullException.ThrowIfNull(logger);

        RepositoryDirectory = TrustedPath.NormalizeAbsolutePath(repositoryDirectory);
        InstalledDirectory = Path.Combine(RepositoryDirectory, InstalledDirectoryName);
        TransactionDirectory = Path.Combine(RepositoryDirectory, TransactionDirectoryName);
        MetadataPath = Path.Combine(RepositoryDirectory, RepositoryFileName);
        _fileSystemOperations = fileSystemOperations;
        _pathResolver = new TrustedPathResolver(fileSystemOperations);
        _logger = logger;
    }

    public BackupRepositoryMetadata LoadOrCreateMetadata()
    {
        _fileSystemOperations.EnsureDirectory(RepositoryDirectory);

        bool created = EnsureMetadataExists();

        EnsureRegularFile(MetadataPath, "Backup repository metadata");

        BackupRepositoryMetadata metadata = ReadMetadataCore();

        ValidateMetadata(metadata);

        _fileSystemOperations.EnsureDirectory(InstalledDirectory);

        EnsureRealDirectory(InstalledDirectory, "Installed records directory");

        if (created)
        {
            BackupRepositoryLog.RepositoryInitialized(_logger, RepositoryDirectory, metadata.RepositoryId);
        }
        else
        {
            BackupRepositoryLog.RepositoryLoaded(_logger, RepositoryDirectory, metadata.RepositoryId);
        }

        return metadata;
    }

    public string GetInstallDirectory(string installId)
    {
        InstallRecordValidator.ValidateInstallId(installId);

        return Path.Combine(InstalledDirectory, installId);
    }

    public InstallRecordEntry ReadRecord(string installId)
    {
        BackupRepositoryMetadata metadata = LoadOrCreateMetadata();
        string installDirectory = GetInstallDirectory(installId);

        EnsureRealDirectory(installDirectory, "Install directory");

        InstallRecord record = ReadRecordCore(installDirectory, metadata.RepositoryId);

        if (!TrustedPath.PathComparer.Equals(record.Id, installId))
        {
            throw new InvalidDataException("Installed directory name does not match its install record ID.");
        }

        return new InstallRecordEntry(installDirectory, record);
    }

    public IReadOnlyList<InstallRecordEntry> ListRecords()
    {
        BackupRepositoryMetadata metadata = LoadOrCreateMetadata();
        var records = new List<InstallRecordEntry>();

        foreach (string directory in Directory.EnumerateDirectories(InstalledDirectory))
        {
            string installDirectory = TrustedPath.NormalizeAbsolutePath(directory);

            EnsureRealDirectory(installDirectory, "Install directory");

            InstallRecord record = ReadRecordCore(installDirectory, metadata.RepositoryId);
            string expectedDirectory = GetInstallDirectory(record.Id);

            if (!TrustedPath.PathsEqual(installDirectory, expectedDirectory))
            {
                throw new InvalidDataException("Installed directory name does not match its install record ID.");
            }

            records.Add(new InstallRecordEntry(installDirectory, record));
        }

        InstallRecordValidator.ValidateAll(records.Select(entry => entry.Record), metadata.RepositoryId);

        InstallRecordEntry[] ordered =
        [
            .. records
                .OrderByDescending(entry => entry.Record.InstallSequence)
                .ThenBy(entry => entry.Record.Id, StringComparer.Ordinal),
        ];

        BackupRepositoryLog.InstallRecordsLoaded(_logger, ordered.Length, metadata.RepositoryId);

        return ordered;
    }

    public void WritePreparedRecord(InstallRecord record, string preparedInstallDirectory)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentException.ThrowIfNullOrWhiteSpace(preparedInstallDirectory);

        BackupRepositoryMetadata metadata = LoadOrCreateMetadata();

        InstallRecordValidator.Validate(record, metadata.RepositoryId);

        IReadOnlyList<InstallRecordEntry> existingRecords = ListRecords();

        if (existingRecords.Any(entry => TrustedPath.PathComparer.Equals(entry.Record.Id, record.Id)))
        {
            throw new IOException($"Install record already exists: {record.Id}");
        }

        InstallRecordValidator.ValidateAll(
            [.. existingRecords.Select(entry => entry.Record), record],
            metadata.RepositoryId);

        string resolvedInstallDirectory = ResolvePreparedInstallDirectory(preparedInstallDirectory);

        _fileSystemOperations.EnsureDirectory(resolvedInstallDirectory);

        EnsureRealDirectory(resolvedInstallDirectory, "Install directory");

        string recordPath = Path.Combine(resolvedInstallDirectory, RecordFileName);
        V1InstallRecordDocument document = V1BackupMapper.Map(record);

        WriteJson(
            recordPath,
            document,
            V1BackupJsonContext.Default.V1InstallRecordDocument,
            FileDestinationMode.CreateNew);

        BackupRepositoryLog.InstallRecordWritten(_logger, record.Id, resolvedInstallDirectory);
    }

    public void CommitInstall(string preparedInstallDirectory, string installId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(preparedInstallDirectory);

        BackupRepositoryMetadata metadata = LoadOrCreateMetadata();
        string sourceDirectory = ResolveExistingTransactionChild(preparedInstallDirectory, "Prepared install");
        InstallRecord record = ReadRecordCore(sourceDirectory, metadata.RepositoryId);

        if (!TrustedPath.PathComparer.Equals(record.Id, installId))
        {
            throw new InvalidDataException("Prepared install record ID does not match the requested install ID.");
        }

        ValidatePreparedBackups(sourceDirectory, record);

        string destinationDirectory = GetInstallDirectory(installId);

        if (TryGetAttributes(destinationDirectory, out _))
        {
            throw new IOException($"Install record already exists: {installId}");
        }

        _fileSystemOperations.MoveDirectory(sourceDirectory, destinationDirectory);

        BackupRepositoryLog.InstallCommitted(_logger, installId);
    }

    private void ValidatePreparedBackups(string preparedInstallDirectory, InstallRecord record)
    {
        foreach (InstallRecordPatchedFile file in record.PatchedFiles)
        {
            string backupPath = _pathResolver.ResolveWithinDirectory(
                preparedInstallDirectory,
                file.BackupRelativePath);

            EnsureRegularFile(backupPath, "Prepared backup");

            FileIntegrity actual = _fileSystemOperations.ComputeFileIntegrity(backupPath);

            if (!file.BackupFile.Matches(actual))
            {
                throw new InvalidDataException($"Prepared backup file integrity does not match: {backupPath}");
            }
        }
    }

    private bool EnsureMetadataExists()
    {
        if (TryGetAttributes(MetadataPath, out _))
        {
            return false;
        }

        var metadata = new BackupRepositoryMetadata(
            CurrentRepositoryFormatVersion,
            Guid.NewGuid().ToString("N"));

        try
        {
            WriteJson(
                MetadataPath,
                V1BackupMapper.Map(metadata),
                V1BackupJsonContext.Default.V1RepositoryDocument,
                FileDestinationMode.CreateNew);

            return true;
        }
        catch (IOException) when (TryGetAttributes(MetadataPath, out _))
        {
            return false;
        }
    }

    private BackupRepositoryMetadata ReadMetadataCore()
    {
        V1RepositoryDocument document = ReadJson(
            MetadataPath,
            V1BackupJsonContext.Default.V1RepositoryDocument,
            "Backup repository metadata");

        return V1BackupMapper.Map(document);
    }

    private InstallRecord ReadRecordCore(string installDirectory, string repositoryId)
    {
        string recordPath = Path.Combine(installDirectory, RecordFileName);

        EnsureRegularFile(recordPath, "Install record");

        V1InstallRecordDocument document = ReadJson(
            recordPath,
            V1BackupJsonContext.Default.V1InstallRecordDocument,
            "Install record");

        InstallRecord record;

        try
        {
            record = V1BackupMapper.Map(document);
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception exception) when (exception is ArgumentException or OverflowException)
        {
            throw new InvalidDataException("Install record contains invalid version 1 data.", exception);
        }

        InstallRecordValidator.Validate(record, repositoryId);

        return record;
    }

    private string ResolvePreparedInstallDirectory(string preparedInstallDirectory)
    {
        string resolvedDirectory = TrustedPath.NormalizeAbsolutePath(preparedInstallDirectory);

        if (!TryGetAttributes(TransactionDirectory, out FileAttributes transactionAttributes) ||
            !transactionAttributes.HasFlag(FileAttributes.Directory) ||
            transactionAttributes.HasFlag(FileAttributes.ReparsePoint) ||
            TrustedPath.PathsEqual(resolvedDirectory, TransactionDirectory) ||
            !TrustedPath.IsWithinRoot(resolvedDirectory, TransactionDirectory))
        {
            throw new InvalidOperationException(
                "Prepared install records must be written inside the active transaction.");
        }

        string relativePath = Path.GetRelativePath(TransactionDirectory, resolvedDirectory);

        return _pathResolver.ResolveWithinDirectory(TransactionDirectory, relativePath);
    }

    private string ResolveExistingTransactionChild(string path, string description)
    {
        EnsureRealDirectory(TransactionDirectory, "Transaction directory");

        string fullPath = TrustedPath.NormalizeAbsolutePath(path);

        if (TrustedPath.PathsEqual(fullPath, TransactionDirectory) ||
            !TrustedPath.IsWithinRoot(fullPath, TransactionDirectory))
        {
            throw new InvalidOperationException($"{description} directory is outside the active transaction.");
        }

        string relativePath = Path.GetRelativePath(TransactionDirectory, fullPath);
        string resolvedPath = _pathResolver.ResolveWithinDirectory(TransactionDirectory, relativePath);

        EnsureRealDirectory(resolvedPath, $"{description} directory");

        return resolvedPath;
    }

    private void ValidateMetadata(BackupRepositoryMetadata metadata)
    {
        if (metadata.FormatVersion != CurrentRepositoryFormatVersion)
        {
            throw new NotSupportedException($"Unsupported backup repository format: {metadata.FormatVersion}.");
        }

        if (string.IsNullOrWhiteSpace(metadata.RepositoryId))
        {
            throw new InvalidDataException("Backup repository ID must not be empty.");
        }
    }

    private void EnsureRegularFile(string path, string description)
    {
        FileAttributes attributes = _fileSystemOperations.GetAttributes(path);

        if (attributes.HasFlag(FileAttributes.Directory) || attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            throw new InvalidDataException($"{description} must be a regular file: {path}");
        }
    }

    private void EnsureRealDirectory(string path, string description)
    {
        FileAttributes attributes = _fileSystemOperations.GetAttributes(path);

        if (!attributes.HasFlag(FileAttributes.Directory) || attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            throw new InvalidDataException($"{description} must be a real directory: {path}");
        }
    }

    private bool TryGetAttributes(string path, out FileAttributes attributes)
    {
        try
        {
            attributes = _fileSystemOperations.GetAttributes(path);

            return true;
        }
        catch (FileNotFoundException)
        {
            attributes = default;

            return false;
        }
        catch (DirectoryNotFoundException)
        {
            attributes = default;

            return false;
        }
    }

    private T ReadJson<T>(string path, JsonTypeInfo<T> typeInfo, string description)
    {
        try
        {
            using Stream stream = _fileSystemOperations.OpenRead(path);

            return JsonSerializer.Deserialize(stream, typeInfo) ??
                   throw new InvalidDataException($"{description} could not be read: {path}");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"{description} contains invalid JSON: {path}", exception);
        }
    }

    private void WriteJson<T>(string path, T value, JsonTypeInfo<T> typeInfo, FileDestinationMode mode)
    {
        _fileSystemOperations.WriteFileAtomically(
            path,
            mode,
            stream => JsonSerializer.Serialize(stream, value, typeInfo));
    }
}

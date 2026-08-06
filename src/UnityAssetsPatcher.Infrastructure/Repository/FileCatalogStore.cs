using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.Logging;
using UnityAssetsPatcher.Application.Repository;
using UnityAssetsPatcher.Application.IO;

namespace UnityAssetsPatcher.Infrastructure.Repository;

internal sealed class FileCatalogStore
{
    public const int CurrentRepositoryFormatVersion = 2;
    public const int LegacyRepositoryFormatVersion = 1;
    public const string RepositoryFileName = "repository.json";
    public const string InstalledDirectoryName = "installed";
    public const string TransactionDirectoryName = ".temp";
    public const string RecordFileName = "record.json";

    public string RepositoryDirectory { get; }
    public string TransactionDirectory { get; }

    private string MetadataPath { get; }

    private readonly IFileSystemOperations _fileSystemOperations;
    private readonly ILogger<FileCatalogStore> _logger;

    public FileCatalogStore(
        string repositoryDirectory,
        IFileSystemOperations fileSystemOperations,
        ILogger<FileCatalogStore> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryDirectory);
        ArgumentNullException.ThrowIfNull(fileSystemOperations);
        ArgumentNullException.ThrowIfNull(logger);

        RepositoryDirectory = TrustedPath.NormalizeAbsolutePath(repositoryDirectory);
        TransactionDirectory = Path.Combine(RepositoryDirectory, TransactionDirectoryName);
        MetadataPath = Path.Combine(RepositoryDirectory, RepositoryFileName);
        _fileSystemOperations = fileSystemOperations;
        _logger = logger;
    }

    public RepositoryMetadata LoadOrCreateMetadata()
    {
        _fileSystemOperations.EnsureDirectory(RepositoryDirectory);

        bool created = EnsureMetadataExists();

        EnsureRegularFile(MetadataPath, "Backup repository metadata");

        RepositoryMetadata metadata = ReadMetadataCore();

        ValidateMetadata(metadata);

        if (metadata.FormatVersion == LegacyRepositoryFormatVersion)
        {
            _fileSystemOperations.EnsureDirectory(GetLegacyInstalledDirectory());

            EnsureRealDirectory(GetLegacyInstalledDirectory(), "Installed records directory");
        }

        if (created)
        {
            RepositoryLog.RepositoryInitialized(_logger, RepositoryDirectory, metadata.RepositoryId);
        }
        else
        {
            RepositoryLog.RepositoryLoaded(_logger, RepositoryDirectory, metadata.RepositoryId);
        }

        return metadata;
    }

    public string GetLegacyInstallDirectory(string installId)
    {
        ValidateLegacyInstallId(installId);

        return Path.Combine(GetLegacyInstalledDirectory(), installId);
    }

    public LegacyInstallRecordEntry ReadLegacyRecord(string installId)
    {
        RepositoryMetadata metadata = LoadOrCreateMetadata();

        if (metadata.FormatVersion == FileCatalogStore.CurrentRepositoryFormatVersion)
        {
            throw new NotSupportedException("Version 1 install records are not available in repository format 2.");
        }

        string installDirectory = GetLegacyInstallDirectory(installId);

        EnsureRealDirectory(installDirectory, "Install directory");

        LegacyInstallRecord record = ReadLegacyRecordCore(installDirectory, metadata.RepositoryId);

        if (!TrustedPath.PathComparer.Equals(record.Id, installId))
        {
            throw new InvalidDataException("Installed directory name does not match its install record ID.");
        }

        return new LegacyInstallRecordEntry(installDirectory, record);
    }

    public IReadOnlyList<LegacyInstallRecordEntry> ListLegacyRecords()
    {
        RepositoryMetadata metadata = LoadOrCreateMetadata();

        if (metadata.FormatVersion == FileCatalogStore.CurrentRepositoryFormatVersion)
        {
            return [];
        }

        var records = new List<LegacyInstallRecordEntry>();

        foreach (string directory in Directory.EnumerateDirectories(GetLegacyInstalledDirectory()))
        {
            string installDirectory = TrustedPath.NormalizeAbsolutePath(directory);

            EnsureRealDirectory(installDirectory, "Install directory");

            LegacyInstallRecord record = ReadLegacyRecordCore(installDirectory, metadata.RepositoryId);
            string expectedDirectory = GetLegacyInstallDirectory(record.Id);

            if (!TrustedPath.PathsEqual(installDirectory, expectedDirectory))
            {
                throw new InvalidDataException("Installed directory name does not match its install record ID.");
            }

            records.Add(new LegacyInstallRecordEntry(installDirectory, record));
        }

        LegacyInstallRecordEntry[] ordered =
        [
            .. records
                .OrderByDescending(entry => entry.Record.InstallSequence)
                .ThenBy(entry => entry.Record.Id, StringComparer.Ordinal),
        ];

        RepositoryLog.InstallRecordsLoaded(_logger, ordered.Length, metadata.RepositoryId);

        return ordered;
    }

    private bool EnsureMetadataExists()
    {
        if (TryGetAttributes(MetadataPath, out _))
        {
            return false;
        }

        var metadata = new RepositoryMetadata(
            CurrentRepositoryFormatVersion,
            Guid.NewGuid().ToString("N"));

        try
        {
            WriteJson(
                MetadataPath,
                RepositoryJsonMapper.Map(metadata),
                V1RepositoryJsonContext.Default.V1RepositoryDocument,
                FileDestinationMode.CreateNew);

            return true;
        }
        catch (IOException) when (TryGetAttributes(MetadataPath, out _))
        {
            return false;
        }
    }

    private RepositoryMetadata ReadMetadataCore()
    {
        V1RepositoryDocument document = ReadJson(
            MetadataPath,
            V1RepositoryJsonContext.Default.V1RepositoryDocument,
            "Backup repository metadata");

        return RepositoryJsonMapper.Map(document);
    }

    private LegacyInstallRecord ReadLegacyRecordCore(string installDirectory, string repositoryId)
    {
        string recordPath = Path.Combine(installDirectory, RecordFileName);

        EnsureRegularFile(recordPath, "Install record");

        V1InstallRecordDocument document = ReadJson(
            recordPath,
            V1RepositoryJsonContext.Default.V1InstallRecordDocument,
            "Install record");

        LegacyInstallRecord record;

        try
        {
            record = RepositoryJsonMapper.Map(document);
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception exception) when (exception is ArgumentException or OverflowException)
        {
            throw new InvalidDataException("Install record contains invalid version 1 data.", exception);
        }

        if (!string.Equals(record.RepositoryId, repositoryId, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Legacy install record does not belong to this backup repository.");
        }

        return record;
    }

    private void ValidateMetadata(RepositoryMetadata metadata)
    {
        if (metadata.FormatVersion is not (LegacyRepositoryFormatVersion or CurrentRepositoryFormatVersion))
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

    private string GetLegacyInstalledDirectory()
    {
        return Path.Combine(RepositoryDirectory, InstalledDirectoryName);
    }

    private static void ValidateLegacyInstallId(string installId)
    {
        if (!TrustedPath.TryNormalizeRelativePath(installId, out string normalized) ||
            normalized.Contains(Path.DirectorySeparatorChar) ||
            normalized.Contains(Path.AltDirectorySeparatorChar))
        {
            throw new InvalidDataException($"Invalid install ID: {installId}");
        }
    }
}

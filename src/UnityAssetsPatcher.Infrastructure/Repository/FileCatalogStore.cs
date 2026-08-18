using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.Logging;
using UnityAssetsPatcher.Application.Repository;
using UnityAssetsPatcher.Application.IO;

namespace UnityAssetsPatcher.Infrastructure.Repository;

internal sealed class FileCatalogStore
{
    public const int CurrentRepositoryFormatVersion = 2;

    private readonly FileRepositoryLayout _layout;
    private readonly IFileSystemOperations _fileSystemOperations;
    private readonly ILogger<FileCatalogStore> _logger;

    public FileCatalogStore(
        FileRepositoryLayout layout,
        IFileSystemOperations fileSystemOperations,
        ILogger<FileCatalogStore> logger)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(fileSystemOperations);
        ArgumentNullException.ThrowIfNull(logger);

        _layout = layout;
        _fileSystemOperations = fileSystemOperations;
        _logger = logger;
    }

    public RepositoryMetadata LoadOrCreateMetadata()
    {
        _fileSystemOperations.EnsureDirectory(_layout.RepositoryDirectory);

        bool created = EnsureMetadataExists();

        EnsureRegularFile(_layout.MetadataPath, "Backup repository metadata");

        RepositoryMetadata metadata = ReadMetadataCore();

        ValidateMetadata(metadata);

        if (created)
        {
            RepositoryLog.RepositoryInitialized(_logger, _layout.RepositoryDirectory, metadata.RepositoryId);
        }
        else
        {
            RepositoryLog.RepositoryLoaded(_logger, _layout.RepositoryDirectory, metadata.RepositoryId);
        }

        return metadata;
    }

    private bool EnsureMetadataExists()
    {
        if (TryGetAttributes(_layout.MetadataPath, out _))
        {
            return false;
        }

        var metadata = new RepositoryMetadata(
            CurrentRepositoryFormatVersion,
            Guid.NewGuid().ToString("N"));

        try
        {
            WriteJson(
                _layout.MetadataPath,
                RepositoryJsonMapper.Map(metadata),
                RepositoryCatalogJsonContext.Default.RepositoryDocument,
                FileDestinationMode.CreateNew);

            return true;
        }
        catch (IOException) when (TryGetAttributes(_layout.MetadataPath, out _))
        {
            return false;
        }
    }

    private RepositoryMetadata ReadMetadataCore()
    {
        RepositoryDocument document = ReadJson(
            _layout.MetadataPath,
            RepositoryCatalogJsonContext.Default.RepositoryDocument,
            "Backup repository metadata");

        return RepositoryJsonMapper.Map(document);
    }

    private void ValidateMetadata(RepositoryMetadata metadata)
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

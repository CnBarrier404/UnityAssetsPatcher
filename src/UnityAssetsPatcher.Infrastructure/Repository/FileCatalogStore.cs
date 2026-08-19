using Microsoft.Extensions.Logging;
using UnityAssetsPatcher.Application.Repository;
using UnityAssetsPatcher.Application.IO;

namespace UnityAssetsPatcher.Infrastructure.Repository;

internal sealed record RepositoryDocument(int FormatVersion, string? RepositoryId);

internal sealed class FileCatalogStore
{
    public const int CurrentRepositoryFormatVersion = 2;

    private readonly FileRepositoryLayout _layout;
    private readonly IFileSystemOperations _fileSystemOperations;
    private readonly RepositoryFileSystem _repositoryFileSystem;
    private readonly RepositoryJsonPersistence _jsonPersistence;
    private readonly ILogger<FileCatalogStore> _logger;

    public FileCatalogStore(
        FileRepositoryLayout layout,
        IFileSystemOperations fileSystemOperations,
        RepositoryFileSystem repositoryFileSystem,
        RepositoryJsonPersistence jsonPersistence,
        ILogger<FileCatalogStore> logger)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(fileSystemOperations);
        ArgumentNullException.ThrowIfNull(repositoryFileSystem);
        ArgumentNullException.ThrowIfNull(jsonPersistence);
        ArgumentNullException.ThrowIfNull(logger);

        _layout = layout;
        _fileSystemOperations = fileSystemOperations;
        _repositoryFileSystem = repositoryFileSystem;
        _jsonPersistence = jsonPersistence;
        _logger = logger;
    }

    public RepositoryMetadata LoadOrCreateMetadata()
    {
        _fileSystemOperations.EnsureDirectory(_layout.RepositoryDirectory);

        bool created = EnsureMetadataExists();

        _repositoryFileSystem.EnsureRegularFile(_layout.MetadataPath, "Backup repository metadata");

        RepositoryDocument document = _jsonPersistence.Read(
            _layout.MetadataPath,
            RepositoryJsonContext.Default.RepositoryDocument,
            "Backup repository metadata");
        var metadata = new RepositoryMetadata(
            document.FormatVersion,
            document.RepositoryId ??
            throw new InvalidDataException("Backup repository data is invalid: repository ID is missing."));

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
        if (_repositoryFileSystem.TryGetAttributes(_layout.MetadataPath, out _))
        {
            return false;
        }

        var metadata = new RepositoryMetadata(
            CurrentRepositoryFormatVersion,
            Guid.NewGuid().ToString("N"));

        try
        {
            _jsonPersistence.Write(
                _layout.MetadataPath,
                new RepositoryDocument(metadata.FormatVersion, metadata.RepositoryId),
                RepositoryJsonContext.Default.RepositoryDocument,
                FileDestinationMode.CreateNew);

            return true;
        }
        catch (IOException) when (_repositoryFileSystem.TryGetAttributes(_layout.MetadataPath, out _))
        {
            return false;
        }
    }

    private static void ValidateMetadata(RepositoryMetadata metadata)
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
}

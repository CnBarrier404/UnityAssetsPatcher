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
        EnsureRepositoryDirectory();

        bool created = EnsureMetadataExists();

        _repositoryFileSystem.EnsureRegularFile(_layout.MetadataPath, "Backup repository metadata");

        RepositoryDocument document = _jsonPersistence.Read(
            _layout.MetadataPath,
            RepositoryJsonContext.Default.RepositoryDocument,
            "Backup repository metadata");
        ValidateFormatVersion(document.FormatVersion);

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

    private void EnsureRepositoryDirectory()
    {
        if (!_repositoryFileSystem.TryGetAttributes(_layout.RepositoryDirectory, out FileAttributes attributes))
        {
            _fileSystemOperations.EnsureDirectory(_layout.RepositoryDirectory);

            return;
        }

        if (!attributes.HasFlag(FileAttributes.Directory) || attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            throw new InvalidDataException(
                $"Backup repository must be a real directory: {_layout.RepositoryDirectory}");
        }
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
        ValidateFormatVersion(metadata.FormatVersion);

        if (string.IsNullOrWhiteSpace(metadata.RepositoryId))
        {
            throw new InvalidDataException("Backup repository ID must not be empty.");
        }
    }

    private static void ValidateFormatVersion(int formatVersion)
    {
        if (formatVersion != CurrentRepositoryFormatVersion)
        {
            throw new UnsupportedRepositoryFormatException(
                formatVersion,
                CurrentRepositoryFormatVersion);
        }
    }
}

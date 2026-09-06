using UnityAssetsPatcher.Application.Repository;
using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Domain.Integrity;

namespace UnityAssetsPatcher.Infrastructure.Repository;

internal sealed class BaseSnapshotStore : IBaseSnapshotStore
{
    public string GamesDirectory => _layout.GamesDirectory;

    private readonly FileRepositoryLayout _layout;
    private readonly IFileSystemOperations _fileSystemOperations;
    private readonly RepositoryFileSystem _repositoryFileSystem;
    private readonly RepositoryJsonPersistence _jsonPersistence;

    public BaseSnapshotStore(
        FileRepositoryLayout layout,
        IFileSystemOperations fileSystemOperations,
        RepositoryFileSystem repositoryFileSystem,
        RepositoryJsonPersistence jsonPersistence)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(fileSystemOperations);
        ArgumentNullException.ThrowIfNull(repositoryFileSystem);
        ArgumentNullException.ThrowIfNull(jsonPersistence);

        _layout = layout;
        _fileSystemOperations = fileSystemOperations;
        _repositoryFileSystem = repositoryFileSystem;
        _jsonPersistence = jsonPersistence;
    }

    public BaseCatalog? TryReadCatalog(string gameInstanceFingerprint)
    {
        try
        {
            return ReadCatalog(gameInstanceFingerprint);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
    }

    public BaseCatalog ReadCatalog(string gameInstanceFingerprint)
    {
        string normalizedFingerprint =
            RepositoryFileSystem.NormalizeIdentifier(gameInstanceFingerprint, nameof(gameInstanceFingerprint));

        string catalogPath = ResolveExistingCatalogPath(normalizedFingerprint);

        BaseCatalog catalog = _jsonPersistence.Read(
            catalogPath,
            RepositoryJsonContext.Default.BaseCatalog,
            "Base catalog");

        return !TrustedPath.PathComparer.Equals(catalog.GameInstanceFingerprint, normalizedFingerprint)
            ? throw new InvalidDataException("Base catalog game instance fingerprint does not match its directory.")
            : catalog;
    }

    public void WriteCatalog(BaseCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        string normalizedFingerprint = RepositoryFileSystem.NormalizeIdentifier(
            catalog.GameInstanceFingerprint,
            nameof(catalog.GameInstanceFingerprint));
        string baseDirectory = EnsureBaseDirectory(normalizedFingerprint);
        string catalogPath = ResolveWithinBaseDirectory(
            baseDirectory,
            _layout.GetBaseCatalogPath(normalizedFingerprint));

        _jsonPersistence.Write(
            catalogPath,
            catalog,
            RepositoryJsonContext.Default.BaseCatalog,
            FileDestinationMode.CreateOrReplace);
    }

    public string GetBaseDirectory(string gameInstanceFingerprint)
    {
        string normalizedFingerprint = RepositoryFileSystem.NormalizeIdentifier(
            gameInstanceFingerprint,
            nameof(gameInstanceFingerprint));
        string gameDirectory = _layout.GetGameDirectory(normalizedFingerprint);

        if (!TrustedPath.IsWithinRoot(gameDirectory, _layout.GamesDirectory) ||
            TrustedPath.PathsEqual(gameDirectory, _layout.GamesDirectory))
        {
            throw new InvalidOperationException("The base snapshot directory is outside the games directory.");
        }

        return _layout.GetBaseDirectory(normalizedFingerprint);
    }

    public string ResolveFilePath(string gameInstanceFingerprint, string relativePath)
    {
        string baseDirectory =
            _repositoryFileSystem.ResolveExistingDirectory(GetBaseDirectory(gameInstanceFingerprint));
        string normalizedRelativePath = NormalizeRelativePath(relativePath);
        string filesPath = Path.Combine(
            _layout.GetBaseFilesDirectory(
                RepositoryFileSystem.NormalizeIdentifier(
                    gameInstanceFingerprint,
                    nameof(gameInstanceFingerprint))),
            normalizedRelativePath);

        return ResolveWithinBaseDirectory(baseDirectory, filesPath);
    }

    public FileIntegrity StoreVerifiedCopy(
        string gameInstanceFingerprint,
        string relativePath,
        string sourcePath)
    {
        string normalizedFingerprint = RepositoryFileSystem.NormalizeIdentifier(
            gameInstanceFingerprint,
            nameof(gameInstanceFingerprint));
        string source = TrustedPath.NormalizeAbsolutePath(sourcePath);
        _repositoryFileSystem.EnsureRegularFile(source, "Base snapshot source");
        string baseDirectory = EnsureBaseDirectory(normalizedFingerprint);
        string filesDirectory = ResolveWithinBaseDirectory(
            baseDirectory,
            _layout.GetBaseFilesDirectory(normalizedFingerprint));
        _fileSystemOperations.EnsureDirectory(filesDirectory);
        _repositoryFileSystem.EnsureRealDirectory(filesDirectory, "Base files directory");

        string destination = ResolveWithinBaseDirectory(
            baseDirectory,
            Path.Combine(
                _layout.GetBaseFilesDirectory(normalizedFingerprint),
                NormalizeRelativePath(relativePath)));
        string destinationDirectory = Path.GetDirectoryName(destination) ??
                                      throw new IOException($"Cannot resolve base snapshot directory: {destination}");
        _fileSystemOperations.EnsureDirectory(destinationDirectory);
        _repositoryFileSystem.EnsureRealDirectory(destinationDirectory, "Base snapshot file directory");

        if (TrustedPath.PathsEqual(source, destination))
        {
            throw new IOException("Base snapshot source and destination must be different files.");
        }

        FileIntegrity expected = _fileSystemOperations.ComputeFileIntegrity(source);

        if (_repositoryFileSystem.TryGetAttributes(destination, out _))
        {
            _repositoryFileSystem.EnsureRegularFile(destination, "Base snapshot file");
            FileIntegrity existing = _fileSystemOperations.ComputeFileIntegrity(destination);

            if (expected.Matches(existing))
            {
                return existing;
            }

            throw new InvalidDataException(
                $"Base snapshot file already exists with different integrity: {destination}");
        }

        bool destinationCreated = false;

        try
        {
            _fileSystemOperations.CopyFileAtomically(source, destination, FileDestinationMode.CreateNew);
            destinationCreated = true;

            FileIntegrity actual = _fileSystemOperations.ComputeFileIntegrity(destination);
            FileIntegrity sourceAfter = _fileSystemOperations.ComputeFileIntegrity(source);

            if (!expected.Matches(actual) || !expected.Matches(sourceAfter))
            {
                throw new IOException($"Base snapshot verification failed: {source}");
            }

            return actual;
        }
        catch (Exception failure)
        {
            if (destinationCreated)
            {
                try
                {
                    _fileSystemOperations.DeleteFile(destination);
                }
                catch (Exception cleanupFailure)
                {
                    throw new AggregateException(failure, cleanupFailure);
                }
            }

            throw;
        }
    }

    public FileIntegrity VerifyFile(
        string gameInstanceFingerprint,
        string relativePath,
        FileIntegrity expected)
    {
        ArgumentNullException.ThrowIfNull(expected);

        string path = ResolveFilePath(gameInstanceFingerprint, relativePath);
        _repositoryFileSystem.EnsureRegularFile(path, "Base snapshot file");
        FileIntegrity actual = _fileSystemOperations.ComputeFileIntegrity(path);

        return !expected.Matches(actual)
            ? throw new InvalidDataException($"Base snapshot file integrity does not match: {path}")
            : actual;
    }

    private string EnsureBaseDirectory(string gameInstanceFingerprint)
    {
        _fileSystemOperations.EnsureDirectory(_layout.GamesDirectory);
        _repositoryFileSystem.EnsureRealDirectory(_layout.GamesDirectory, "Games directory");

        string gameDirectory = _layout.GetGameDirectory(gameInstanceFingerprint);
        _fileSystemOperations.EnsureDirectory(gameDirectory);
        _repositoryFileSystem.EnsureRealDirectory(gameDirectory, "Game snapshot directory");

        string baseDirectory = GetBaseDirectory(gameInstanceFingerprint);
        _fileSystemOperations.EnsureDirectory(baseDirectory);
        _repositoryFileSystem.EnsureRealDirectory(baseDirectory, "Base snapshot directory");

        return baseDirectory;
    }

    private string ResolveExistingCatalogPath(string gameInstanceFingerprint)
    {
        string baseDirectory =
            _repositoryFileSystem.ResolveExistingDirectory(GetBaseDirectory(gameInstanceFingerprint));
        string catalogPath = ResolveWithinBaseDirectory(
            baseDirectory,
            _layout.GetBaseCatalogPath(gameInstanceFingerprint));
        _repositoryFileSystem.EnsureRegularFile(catalogPath, "Base catalog");

        return catalogPath;
    }

    private string ResolveWithinBaseDirectory(string baseDirectory, string path)
    {
        return _repositoryFileSystem.ResolveWithinDirectory(
            baseDirectory,
            Path.GetRelativePath(baseDirectory, path));
    }

    private static string NormalizeRelativePath(string relativePath)
    {
        return !TrustedPath.TryNormalizeRelativePath(relativePath, out string normalizedPath)
            ? throw new IOException($"The relative path is not trusted: '{relativePath}'.")
            : normalizedPath;
    }
}

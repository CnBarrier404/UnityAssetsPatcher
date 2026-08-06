using UnityAssetsPatcher.Application.Repository;
using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Domain.Integrity;

namespace UnityAssetsPatcher.Infrastructure.Repository;

internal sealed class BaseSnapshotStore : IBaseSnapshotStore
{
    public const string GamesDirectoryName = "games";
    public const string BaseDirectoryName = "base";
    public const string CatalogFileName = "catalog.json";
    public const string FilesDirectoryName = "files";

    public string GamesDirectory { get; }

    private readonly IFileSystemOperations _fileSystemOperations;
    private readonly TrustedPathResolver _pathResolver;

    public BaseSnapshotStore(string repositoryDirectory, IFileSystemOperations fileSystemOperations)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryDirectory);
        ArgumentNullException.ThrowIfNull(fileSystemOperations);

        string normalizedRepositoryDirectory = TrustedPath.NormalizeAbsolutePath(repositoryDirectory);
        GamesDirectory = Path.Combine(normalizedRepositoryDirectory, GamesDirectoryName);
        _fileSystemOperations = fileSystemOperations;
        _pathResolver = new TrustedPathResolver(fileSystemOperations);
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
        string normalizedFingerprint = FileCompositionStoreSupport.NormalizeIdentifier(
            gameInstanceFingerprint,
            nameof(gameInstanceFingerprint));
        string catalogPath = ResolveExistingCatalogPath(normalizedFingerprint);
        BaseCatalog catalog = FileCompositionStoreSupport.ReadJson(
            _fileSystemOperations,
            catalogPath,
            RepositoryJsonContext.Default.BaseCatalog,
            "Base catalog");

        if (!TrustedPath.PathComparer.Equals(catalog.GameInstanceFingerprint, normalizedFingerprint))
        {
            throw new InvalidDataException("Base catalog game instance fingerprint does not match its directory.");
        }

        return catalog;
    }

    public void WriteCatalog(BaseCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        string normalizedFingerprint = FileCompositionStoreSupport.NormalizeIdentifier(
            catalog.GameInstanceFingerprint,
            nameof(catalog.GameInstanceFingerprint));
        string baseDirectory = EnsureBaseDirectory(normalizedFingerprint);
        string catalogPath = _pathResolver.ResolveWithinDirectory(baseDirectory, CatalogFileName);

        FileCompositionStoreSupport.WriteJson(
            _fileSystemOperations,
            catalogPath,
            catalog,
            RepositoryJsonContext.Default.BaseCatalog,
            FileDestinationMode.CreateOrReplace);
    }

    public string GetBaseDirectory(string gameInstanceFingerprint)
    {
        string normalizedFingerprint = FileCompositionStoreSupport.NormalizeIdentifier(
            gameInstanceFingerprint,
            nameof(gameInstanceFingerprint));
        string gameDirectory = Path.Combine(GamesDirectory, normalizedFingerprint);

        if (!TrustedPath.IsWithinRoot(gameDirectory, GamesDirectory) ||
            TrustedPath.PathsEqual(gameDirectory, GamesDirectory))
        {
            throw new InvalidOperationException("The base snapshot directory is outside the games directory.");
        }

        return Path.Combine(gameDirectory, BaseDirectoryName);
    }

    public string ResolveFilePath(string gameInstanceFingerprint, string relativePath)
    {
        string baseDirectory = _pathResolver.ResolveExistingDirectory(GetBaseDirectory(gameInstanceFingerprint));
        string normalizedRelativePath = NormalizeRelativePath(relativePath);

        return _pathResolver.ResolveWithinDirectory(
            baseDirectory,
            Path.Combine(FilesDirectoryName, normalizedRelativePath));
    }

    public FileIntegrity StoreVerifiedCopy(
        string gameInstanceFingerprint,
        string relativePath,
        string sourcePath)
    {
        string normalizedFingerprint = FileCompositionStoreSupport.NormalizeIdentifier(
            gameInstanceFingerprint,
            nameof(gameInstanceFingerprint));
        string source = TrustedPath.NormalizeAbsolutePath(sourcePath);
        FileCompositionStoreSupport.EnsureRegularFile(_fileSystemOperations, source, "Base snapshot source");
        string baseDirectory = EnsureBaseDirectory(normalizedFingerprint);
        string filesDirectory = _pathResolver.ResolveWithinDirectory(baseDirectory, FilesDirectoryName);
        _fileSystemOperations.EnsureDirectory(filesDirectory);
        FileCompositionStoreSupport.EnsureRealDirectory(_fileSystemOperations, filesDirectory, "Base files directory");

        string destination = _pathResolver.ResolveWithinDirectory(
            baseDirectory,
            Path.Combine(FilesDirectoryName, NormalizeRelativePath(relativePath)));
        string destinationDirectory = Path.GetDirectoryName(destination) ??
                                      throw new IOException($"Cannot resolve base snapshot directory: {destination}");
        _fileSystemOperations.EnsureDirectory(destinationDirectory);
        FileCompositionStoreSupport.EnsureRealDirectory(
            _fileSystemOperations,
            destinationDirectory,
            "Base snapshot file directory");

        if (TrustedPath.PathsEqual(source, destination))
        {
            throw new IOException("Base snapshot source and destination must be different files.");
        }

        FileIntegrity expected = _fileSystemOperations.ComputeFileIntegrity(source);

        if (FileCompositionStoreSupport.TryGetAttributes(_fileSystemOperations, destination, out _))
        {
            FileCompositionStoreSupport.EnsureRegularFile(_fileSystemOperations, destination, "Base snapshot file");
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
        catch
        {
            if (destinationCreated)
            {
                TryDeleteSnapshot(destination);
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
        FileCompositionStoreSupport.EnsureRegularFile(_fileSystemOperations, path, "Base snapshot file");
        FileIntegrity actual = _fileSystemOperations.ComputeFileIntegrity(path);

        if (!expected.Matches(actual))
        {
            throw new InvalidDataException($"Base snapshot file integrity does not match: {path}");
        }

        return actual;
    }

    private string EnsureBaseDirectory(string gameInstanceFingerprint)
    {
        _fileSystemOperations.EnsureDirectory(GamesDirectory);
        FileCompositionStoreSupport.EnsureRealDirectory(_fileSystemOperations, GamesDirectory, "Games directory");

        string gameDirectory = Path.Combine(GamesDirectory, gameInstanceFingerprint);
        _fileSystemOperations.EnsureDirectory(gameDirectory);
        FileCompositionStoreSupport.EnsureRealDirectory(_fileSystemOperations, gameDirectory,
            "Game snapshot directory");

        string baseDirectory = GetBaseDirectory(gameInstanceFingerprint);
        _fileSystemOperations.EnsureDirectory(baseDirectory);
        FileCompositionStoreSupport.EnsureRealDirectory(_fileSystemOperations, baseDirectory,
            "Base snapshot directory");

        return baseDirectory;
    }

    private string ResolveExistingCatalogPath(string gameInstanceFingerprint)
    {
        string baseDirectory = _pathResolver.ResolveExistingDirectory(GetBaseDirectory(gameInstanceFingerprint));
        string catalogPath = _pathResolver.ResolveWithinDirectory(baseDirectory, CatalogFileName);
        FileCompositionStoreSupport.EnsureRegularFile(_fileSystemOperations, catalogPath, "Base catalog");

        return catalogPath;
    }

    private static string NormalizeRelativePath(string relativePath)
    {
        if (!TrustedPath.TryNormalizeRelativePath(relativePath, out string normalizedPath))
        {
            throw new IOException($"The relative path is not trusted: '{relativePath}'.");
        }

        return normalizedPath;
    }

    private void TryDeleteSnapshot(string path)
    {
        try
        {
            _fileSystemOperations.DeleteFile(path);
        }
        catch { }
    }
}

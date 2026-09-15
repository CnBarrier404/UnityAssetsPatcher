using UnityAssetsPatcher.Application.Repository;
using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Domain.Integrity;

namespace UnityAssetsPatcher.Infrastructure.Repository;

internal sealed class LayerStore : ILayerStore
{
    public string LayersDirectory => _layout.LayersDirectory;

    private readonly FileRepositoryLayout _layout;
    private readonly IFileSystemOperations _fileSystemOperations;
    private readonly RepositoryFileSystem _repositoryFileSystem;
    private readonly RepositoryJsonPersistence _jsonPersistence;

    public LayerStore(
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

    public LayerRecordEntry ReadLayer(string layerId)
    {
        string normalizedLayerId = RepositoryFileSystem.NormalizeIdentifier(layerId, nameof(layerId));
        string layerDirectory = _repositoryFileSystem.ResolveExistingDirectory(GetLayerDirectory(normalizedLayerId));
        LayerRecord record = ReadLayerCore(layerDirectory, normalizedLayerId);

        return !TrustedPath.PathComparer.Equals(record.Id, normalizedLayerId)
            ? throw new InvalidDataException("Layer directory name does not match its layer record ID.")
            : new LayerRecordEntry(layerDirectory, record);
    }

    public IReadOnlyList<LayerRecordEntry> ListLayers()
    {
        if (!_repositoryFileSystem.TryGetAttributes(LayersDirectory, out _))
        {
            return [];
        }

        _repositoryFileSystem.EnsureRealDirectory(LayersDirectory, "Layers directory");

        var layers = new List<LayerRecordEntry>();

        foreach (string directory in Directory.EnumerateDirectories(LayersDirectory))
        {
            string layerDirectory = TrustedPath.NormalizeAbsolutePath(directory);
            _repositoryFileSystem.EnsureRealDirectory(layerDirectory, "Layer directory");
            string layerId = Path.GetFileName(Path.TrimEndingDirectorySeparator(layerDirectory));
            layers.Add(ReadLayer(layerId));
        }

        LayerRecordEntry[] ordered =
        [
            .. layers
                .OrderByDescending(entry => entry.Record.InstallSequence)
                .ThenBy(entry => entry.Record.Id, StringComparer.Ordinal)
        ];

        return ordered;
    }

    public string GetLayerDirectory(string layerId)
    {
        string normalizedLayerId = RepositoryFileSystem.NormalizeIdentifier(layerId, nameof(layerId));
        string layerDirectory = _layout.GetLayerDirectory(normalizedLayerId);

        if (!TrustedPath.IsWithinRoot(layerDirectory, _layout.LayersDirectory) ||
            TrustedPath.PathsEqual(layerDirectory, _layout.LayersDirectory))
        {
            throw new InvalidOperationException("The layer directory is outside the layers directory.");
        }

        return layerDirectory;
    }

    public string ResolvePackagePath(string layerId)
    {
        LayerRecordEntry entry = ReadLayer(layerId);
        string packagePath = _repositoryFileSystem.ResolveWithinDirectory(
            entry.LayerDirectory,
            entry.Record.Package.FileName);
        _repositoryFileSystem.EnsureRegularFile(packagePath, "Layer package");

        return packagePath;
    }

    public FileIntegrity VerifyPackage(string layerId)
    {
        LayerRecordEntry entry = ReadLayer(layerId);
        string packagePath = _repositoryFileSystem.ResolveWithinDirectory(
            entry.LayerDirectory,
            entry.Record.Package.FileName);
        _repositoryFileSystem.EnsureRegularFile(packagePath, "Layer package");
        FileIntegrity actual = _fileSystemOperations.ComputeFileIntegrity(packagePath);

        if (!entry.Record.Package.Integrity.Matches(actual))
        {
            throw new InvalidDataException($"Layer package integrity does not match: {packagePath}");
        }

        return actual;
    }

    public FileIntegrity StoreVerifiedPackage(
        string sourcePath,
        string preparedLayerDirectory,
        LayerPackageInfo package)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentNullException.ThrowIfNull(package);

        string source = TrustedPath.NormalizeAbsolutePath(sourcePath);
        _repositoryFileSystem.EnsureRegularFile(source, "Layer package source");
        FileIntegrity expected = _fileSystemOperations.ComputeFileIntegrity(source);

        if (!package.Integrity.Matches(expected))
        {
            throw new InvalidDataException($"Layer package source integrity does not match: {source}");
        }

        string preparedDirectory = _repositoryFileSystem.ResolvePreparedTransactionChild(
            _layout.TransactionDirectory,
            preparedLayerDirectory,
            "Prepared layer");
        _fileSystemOperations.EnsureDirectory(preparedDirectory);
        _repositoryFileSystem.EnsureRealDirectory(preparedDirectory, "Prepared layer directory");
        string destination = _repositoryFileSystem.ResolveWithinDirectory(preparedDirectory, package.FileName);

        if (TrustedPath.PathsEqual(source, destination))
        {
            throw new IOException("Layer package source and destination must be different files.");
        }

        _fileSystemOperations.CopyFileAtomically(source, destination, FileDestinationMode.CreateNew);

        try
        {
            FileIntegrity actual = _fileSystemOperations.ComputeFileIntegrity(destination);

            return package.Integrity.Matches(actual)
                ? actual
                : throw new IOException($"Layer package verification failed: {source}");
        }
        catch (Exception failure)
        {
            try
            {
                _fileSystemOperations.DeleteFile(destination);
            }
            catch (Exception cleanupFailure)
            {
                throw new AggregateException(failure, cleanupFailure);
            }

            throw;
        }
    }

    public void WritePreparedLayer(LayerRecord record, string preparedLayerDirectory)
    {
        ArgumentNullException.ThrowIfNull(record);

        string preparedDirectory = _repositoryFileSystem.ResolvePreparedTransactionChild(
            _layout.TransactionDirectory,
            preparedLayerDirectory,
            "Prepared layer");
        _fileSystemOperations.EnsureDirectory(preparedDirectory);
        _repositoryFileSystem.EnsureRealDirectory(preparedDirectory, "Prepared layer directory");
        string layerPath = _repositoryFileSystem.ResolveWithinDirectory(
            preparedDirectory,
            FileRepositoryLayout.LayerRecordFileName);

        _jsonPersistence.Write(
            layerPath,
            record,
            RepositoryJsonContext.Default.LayerRecord,
            FileDestinationMode.CreateNew);
    }

    public void CommitLayer(string preparedLayerDirectory, string layerId)
    {
        string normalizedLayerId = RepositoryFileSystem.NormalizeIdentifier(layerId, nameof(layerId));
        string sourceDirectory = _repositoryFileSystem.ResolveExistingTransactionChild(
            _layout.TransactionDirectory,
            preparedLayerDirectory,
            "Prepared layer");
        LayerRecord record = ReadLayerCore(sourceDirectory, normalizedLayerId);

        if (!TrustedPath.PathComparer.Equals(record.Id, normalizedLayerId))
        {
            throw new InvalidDataException("Prepared layer record ID does not match the requested layer ID.");
        }

        ValidatePackage(sourceDirectory, record.Package);
        _fileSystemOperations.EnsureDirectory(_layout.LayersDirectory);
        _repositoryFileSystem.EnsureRealDirectory(_layout.LayersDirectory, "Layers directory");

        string destinationDirectory = GetLayerDirectory(normalizedLayerId);

        if (_repositoryFileSystem.TryGetAttributes(destinationDirectory, out _))
        {
            throw new IOException($"Layer already exists: {normalizedLayerId}");
        }

        _fileSystemOperations.MoveDirectory(sourceDirectory, destinationDirectory);
    }

    public void DeleteLayer(string layerId)
    {
        string layerDirectory = _repositoryFileSystem.ResolveExistingDirectory(GetLayerDirectory(layerId));

        _repositoryFileSystem.EnsureRealDirectory(layerDirectory, "Layer directory");
        _fileSystemOperations.DeleteDirectoryTree(layerDirectory);
    }

    private LayerRecord ReadLayerCore(string layerDirectory, string normalizedLayerId)
    {
        string layerPath = _repositoryFileSystem.ResolveWithinDirectory(
            layerDirectory,
            Path.GetFileName(_layout.GetLayerRecordPath(normalizedLayerId)));
        _repositoryFileSystem.EnsureRegularFile(layerPath, "Layer record");

        return _jsonPersistence.Read(
            layerPath,
            RepositoryJsonContext.Default.LayerRecord,
            "Layer record");
    }

    private void ValidatePackage(string layerDirectory, LayerPackageInfo package)
    {
        string packagePath = _repositoryFileSystem.ResolveWithinDirectory(layerDirectory, package.FileName);
        _repositoryFileSystem.EnsureRegularFile(packagePath, "Layer package");
        FileIntegrity actual = _fileSystemOperations.ComputeFileIntegrity(packagePath);

        if (!package.Integrity.Matches(actual))
        {
            throw new InvalidDataException($"Layer package integrity does not match: {packagePath}");
        }
    }
}

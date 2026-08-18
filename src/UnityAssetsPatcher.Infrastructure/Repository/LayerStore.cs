using UnityAssetsPatcher.Application.Repository;
using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Domain.Integrity;

namespace UnityAssetsPatcher.Infrastructure.Repository;

internal sealed class LayerStore : ILayerStore
{
    public string LayersDirectory => _layout.LayersDirectory;

    private readonly FileRepositoryLayout _layout;
    private readonly IFileSystemOperations _fileSystemOperations;
    private readonly TrustedPathResolver _pathResolver;

    public LayerStore(FileRepositoryLayout layout, IFileSystemOperations fileSystemOperations)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(fileSystemOperations);

        _layout = layout;
        _fileSystemOperations = fileSystemOperations;
        _pathResolver = new TrustedPathResolver(fileSystemOperations);
    }

    public LayerRecordEntry ReadLayer(string layerId)
    {
        string normalizedLayerId = FileCompositionStoreSupport.NormalizeIdentifier(layerId, nameof(layerId));
        string layerDirectory = _pathResolver.ResolveExistingDirectory(GetLayerDirectory(normalizedLayerId));
        LayerRecord record = ReadLayerCore(layerDirectory, normalizedLayerId);

        return !TrustedPath.PathComparer.Equals(record.Id, normalizedLayerId)
            ? throw new InvalidDataException("Layer directory name does not match its layer record ID.")
            : new LayerRecordEntry(layerDirectory, record);
    }

    public IReadOnlyList<LayerRecordEntry> ListLayers()
    {
        if (!FileCompositionStoreSupport.TryGetAttributes(_fileSystemOperations, LayersDirectory,
                out FileAttributes attributes))
        {
            return [];
        }

        if (!attributes.HasFlag(FileAttributes.Directory) || attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            throw new InvalidDataException($"Layers directory must be a real directory: {LayersDirectory}");
        }

        var layers = new List<LayerRecordEntry>();

        foreach (string directory in Directory.EnumerateDirectories(LayersDirectory))
        {
            string layerDirectory = TrustedPath.NormalizeAbsolutePath(directory);
            FileCompositionStoreSupport.EnsureRealDirectory(
                _fileSystemOperations,
                layerDirectory,
                "Layer directory");
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
        string normalizedLayerId = FileCompositionStoreSupport.NormalizeIdentifier(layerId, nameof(layerId));
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
        string packagePath = _pathResolver.ResolveWithinDirectory(
            entry.LayerDirectory,
            entry.Record.Package.FileName);
        FileCompositionStoreSupport.EnsureRegularFile(_fileSystemOperations, packagePath, "Layer package");

        return packagePath;
    }

    public FileIntegrity VerifyPackage(string layerId)
    {
        LayerRecordEntry entry = ReadLayer(layerId);
        string packagePath = _pathResolver.ResolveWithinDirectory(
            entry.LayerDirectory,
            entry.Record.Package.FileName);
        FileCompositionStoreSupport.EnsureRegularFile(_fileSystemOperations, packagePath, "Layer package");
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
        FileCompositionStoreSupport.EnsureRegularFile(_fileSystemOperations, source, "Layer package source");
        FileIntegrity expected = _fileSystemOperations.ComputeFileIntegrity(source);

        if (!package.Integrity.Matches(expected))
        {
            throw new InvalidDataException($"Layer package source integrity does not match: {source}");
        }

        string preparedDirectory = FileCompositionStoreSupport.ResolvePreparedTransactionChild(
            _fileSystemOperations,
            _pathResolver,
            _layout.TransactionDirectory,
            preparedLayerDirectory,
            "Prepared layer");
        _fileSystemOperations.EnsureDirectory(preparedDirectory);
        FileCompositionStoreSupport.EnsureRealDirectory(
            _fileSystemOperations,
            preparedDirectory,
            "Prepared layer directory");
        string destination = _pathResolver.ResolveWithinDirectory(preparedDirectory, package.FileName);

        if (TrustedPath.PathsEqual(source, destination))
        {
            throw new IOException("Layer package source and destination must be different files.");
        }

        _fileSystemOperations.CopyFileAtomically(source, destination, FileDestinationMode.CreateNew);

        FileIntegrity actual = _fileSystemOperations.ComputeFileIntegrity(destination);

        if (!package.Integrity.Matches(actual))
        {
            _fileSystemOperations.DeleteFile(destination);

            throw new IOException($"Layer package verification failed: {source}");
        }

        return actual;
    }

    public void WritePreparedLayer(LayerRecord record, string preparedLayerDirectory)
    {
        ArgumentNullException.ThrowIfNull(record);

        string preparedDirectory = FileCompositionStoreSupport.ResolvePreparedTransactionChild(
            _fileSystemOperations,
            _pathResolver,
            _layout.TransactionDirectory,
            preparedLayerDirectory,
            "Prepared layer");
        _fileSystemOperations.EnsureDirectory(preparedDirectory);
        FileCompositionStoreSupport.EnsureRealDirectory(
            _fileSystemOperations,
            preparedDirectory,
            "Prepared layer directory");
        string layerPath = _pathResolver.ResolveWithinDirectory(
            preparedDirectory,
            FileRepositoryLayout.LayerRecordFileName);

        FileCompositionStoreSupport.WriteJson(
            _fileSystemOperations,
            layerPath,
            record,
            RepositoryJsonContext.Default.LayerRecord,
            FileDestinationMode.CreateNew);
    }

    public void CommitLayer(string preparedLayerDirectory, string layerId)
    {
        string normalizedLayerId = FileCompositionStoreSupport.NormalizeIdentifier(layerId, nameof(layerId));
        string sourceDirectory = FileCompositionStoreSupport.ResolveExistingTransactionChild(
            _fileSystemOperations,
            _pathResolver,
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
        FileCompositionStoreSupport.EnsureRealDirectory(
            _fileSystemOperations,
            _layout.LayersDirectory,
            "Layers directory");

        string destinationDirectory = GetLayerDirectory(normalizedLayerId);

        if (FileCompositionStoreSupport.TryGetAttributes(_fileSystemOperations, destinationDirectory, out _))
        {
            throw new IOException($"Layer already exists: {normalizedLayerId}");
        }

        _fileSystemOperations.MoveDirectory(sourceDirectory, destinationDirectory);
    }

    public void DeleteLayer(string layerId)
    {
        string layerDirectory = _pathResolver.ResolveExistingDirectory(GetLayerDirectory(layerId));

        FileCompositionStoreSupport.EnsureRealDirectory(_fileSystemOperations, layerDirectory, "Layer directory");
        _fileSystemOperations.DeleteDirectoryTree(layerDirectory);
    }

    private LayerRecord ReadLayerCore(string layerDirectory, string normalizedLayerId)
    {
        string layerPath = _pathResolver.ResolveWithinDirectory(
            layerDirectory,
            Path.GetFileName(_layout.GetLayerRecordPath(normalizedLayerId)));
        FileCompositionStoreSupport.EnsureRegularFile(_fileSystemOperations, layerPath, "Layer record");

        return FileCompositionStoreSupport.ReadJson(
            _fileSystemOperations,
            layerPath,
            RepositoryJsonContext.Default.LayerRecord,
            "Layer record");
    }

    private void ValidatePackage(string layerDirectory, LayerPackageInfo package)
    {
        string packagePath = _pathResolver.ResolveWithinDirectory(layerDirectory, package.FileName);
        FileCompositionStoreSupport.EnsureRegularFile(_fileSystemOperations, packagePath, "Layer package");
        FileIntegrity actual = _fileSystemOperations.ComputeFileIntegrity(packagePath);

        if (!package.Integrity.Matches(actual))
        {
            throw new InvalidDataException($"Layer package integrity does not match: {packagePath}");
        }
    }
}

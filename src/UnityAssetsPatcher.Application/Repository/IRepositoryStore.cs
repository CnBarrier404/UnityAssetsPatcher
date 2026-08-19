using UnityAssetsPatcher.Domain.Integrity;

namespace UnityAssetsPatcher.Application.Repository;

public interface IBaseSnapshotStore
{
    public string GamesDirectory { get; }

    public BaseCatalog? TryReadCatalog(string gameInstanceFingerprint);

    public BaseCatalog ReadCatalog(string gameInstanceFingerprint);

    public void WriteCatalog(BaseCatalog catalog);

    public string GetBaseDirectory(string gameInstanceFingerprint);

    public string ResolveFilePath(string gameInstanceFingerprint, string relativePath);

    public FileIntegrity StoreVerifiedCopy(
        string gameInstanceFingerprint,
        string relativePath,
        string sourcePath);

    public FileIntegrity VerifyFile(
        string gameInstanceFingerprint,
        string relativePath,
        FileIntegrity expected);
}

public interface ILayerStore
{
    public string LayersDirectory { get; }

    public LayerRecordEntry ReadLayer(string layerId);

    public IReadOnlyList<LayerRecordEntry> ListLayers();

    public string GetLayerDirectory(string layerId);

    public string ResolvePackagePath(string layerId);

    public FileIntegrity VerifyPackage(string layerId);

    public FileIntegrity StoreVerifiedPackage(
        string sourcePath,
        string preparedLayerDirectory,
        LayerPackageInfo package);

    public void WritePreparedLayer(LayerRecord record, string preparedLayerDirectory);

    public void CommitLayer(string preparedLayerDirectory, string layerId);

    public void DeleteLayer(string layerId);
}

public interface IRepositoryStore
{
    public string RepositoryDirectory { get; }

    public string TransactionDirectory { get; }

    public IBaseSnapshotStore BaseSnapshots { get; }

    public ILayerStore Layers { get; }

    public IRepositoryTransactionStore Transactions { get; }

    public RepositoryMetadata LoadOrCreateMetadata();
}

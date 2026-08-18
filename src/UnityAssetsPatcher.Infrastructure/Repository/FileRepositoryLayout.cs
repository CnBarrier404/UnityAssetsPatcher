using UnityAssetsPatcher.Application.IO;

namespace UnityAssetsPatcher.Infrastructure.Repository;

internal sealed class FileRepositoryLayout
{
    public string RepositoryDirectory { get; }
    public string MetadataPath { get; }
    public string TransactionDirectory { get; }
    public string GamesDirectory { get; }
    public string LayersDirectory { get; }

    internal const string PackageFileName = "package.zip";
    internal const string LayerRecordFileName = "layer.json";
    private const string MetadataFileName = "repository.json";
    private const string TransactionDirectoryName = ".temp";
    private const string GamesDirectoryName = "games";
    private const string BaseDirectoryName = "base";
    private const string BaseCatalogFileName = "catalog.json";
    private const string BaseFilesDirectoryName = "files";
    private const string LayersDirectoryName = "layers";

    public FileRepositoryLayout(string repositoryDirectory)
    {
        RepositoryDirectory = TrustedPath.NormalizeAbsolutePath(repositoryDirectory);
        MetadataPath = Path.Combine(RepositoryDirectory, MetadataFileName);
        TransactionDirectory = Path.Combine(RepositoryDirectory, TransactionDirectoryName);
        GamesDirectory = Path.Combine(RepositoryDirectory, GamesDirectoryName);
        LayersDirectory = Path.Combine(RepositoryDirectory, LayersDirectoryName);
    }

    public string GetGameDirectory(string normalizedGameInstanceFingerprint)
    {
        return Path.Combine(GamesDirectory, normalizedGameInstanceFingerprint);
    }

    public string GetBaseDirectory(string normalizedGameInstanceFingerprint)
    {
        return Path.Combine(GetGameDirectory(normalizedGameInstanceFingerprint), BaseDirectoryName);
    }

    public string GetBaseCatalogPath(string normalizedGameInstanceFingerprint)
    {
        return Path.Combine(GetBaseDirectory(normalizedGameInstanceFingerprint), BaseCatalogFileName);
    }

    public string GetBaseFilesDirectory(string normalizedGameInstanceFingerprint)
    {
        return Path.Combine(GetBaseDirectory(normalizedGameInstanceFingerprint), BaseFilesDirectoryName);
    }

    public string GetLayerDirectory(string normalizedLayerId)
    {
        return Path.Combine(LayersDirectory, normalizedLayerId);
    }

    public string GetLayerRecordPath(string normalizedLayerId)
    {
        return Path.Combine(GetLayerDirectory(normalizedLayerId), LayerRecordFileName);
    }
}

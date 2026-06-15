using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace UnityAssetsPatcher.AssetsTools;

public sealed class AssetsToolsContext : IDisposable
{
    private AssetsManager Manager { get; } = new();

    private readonly Lock _gate = new();
    private readonly string _tpkFilePath;
    private bool _classPackageLoaded;
    private string? _loadedClassDatabaseVersion;
    private bool _disposed;

    public AssetsToolsContext(string tpkFilePath)
    {
        _tpkFilePath = tpkFilePath;
    }

    public AssetsFileInstance LoadAssetsFile(string assetsFilePath)
    {
        lock (_gate)
        {
            EnsureNotDisposed();
            EnsureClassPackageLoaded();

            return Manager.LoadAssetsFile(assetsFilePath, true);
        }
    }

    public AssetTypeValueField GetBaseField(AssetsFileInstance assetsFileInstance, long pathId)
    {
        lock (_gate)
        {
            EnsureNotDisposed();
            EnsureClassDatabaseLoadedFor(assetsFileInstance.file.Metadata.UnityVersion);

            return Manager.GetBaseField(assetsFileInstance, pathId);
        }
    }

    public void UnloadAssetsFile(AssetsFileInstance assetsFileInstance)
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            Manager.UnloadAssetsFile(assetsFileInstance);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Manager.UnloadAll(true);
        }
    }

    private void EnsureClassPackageLoaded()
    {
        if (_classPackageLoaded)
        {
            return;
        }

        if (!File.Exists(_tpkFilePath))
        {
            throw new FileNotFoundException($"TPK file not found: {_tpkFilePath}", _tpkFilePath);
        }

        Manager.LoadClassPackage(_tpkFilePath);
        _classPackageLoaded = true;
    }

    private void EnsureClassDatabaseLoadedFor(string unityVersion)
    {
        EnsureClassPackageLoaded();

        if (string.Equals(_loadedClassDatabaseVersion, unityVersion, StringComparison.Ordinal))
        {
            return;
        }

        Manager.LoadClassDatabaseFromPackage(unityVersion);
        _loadedClassDatabaseVersion = unityVersion;
    }

    private void EnsureNotDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}

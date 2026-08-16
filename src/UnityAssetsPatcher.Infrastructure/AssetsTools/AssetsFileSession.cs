using AssetsTools.NET;
using AssetsTools.NET.Extra;
using UnityAssetsPatcher.Domain.Assets;

namespace UnityAssetsPatcher.Infrastructure.AssetsTools;

internal sealed class AssetsFileSession : IDisposable
{
    public AssetsFile AssetsFile => _instance.file;

    private readonly HashSet<long> _assetPathIds;
    private readonly ClassPackageCache _classPackageCache;
    private readonly AssetsManager _manager;
    private readonly AssetsFileInstance _instance;
    private string? _loadedClassDatabaseVersion;
    private bool _disposed;

    private AssetsFileSession(
        ClassPackageCache classPackageCache,
        AssetsManager manager,
        AssetsFileInstance instance)
    {
        _assetPathIds = [.. instance.file.Metadata.AssetInfos.Select(info => info.PathId)];
        _classPackageCache = classPackageCache;
        _manager = manager;
        _instance = instance;
    }

    public static AssetsFileSession Open(string assetsFilePath, ClassPackageCache classPackageCache)
    {
        ArgumentNullException.ThrowIfNull(classPackageCache);

        if (!File.Exists(assetsFilePath))
        {
            throw new FileNotFoundException($"Assets file not found: {assetsFilePath}", assetsFilePath);
        }

        classPackageCache.EnsureLoaded();

        var manager = new AssetsManager();

        try
        {
            AssetsFileInstance instance = manager.LoadAssetsFile(Path.GetFullPath(assetsFilePath), loadDeps: false);

            return new AssetsFileSession(classPackageCache, manager, instance);
        }
        catch (Exception exception)
        {
            var cleanupExceptions = ResourceCleanup.RunAll(
            [
                () => manager.UnloadAll(unloadClassData: true),
            ]);
            ResourceCleanup.ThrowOrAttach(exception, cleanupExceptions);

            throw;
        }
    }

    public AssetTypeValueField GetBaseField(AssetPathId pathId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        EnsureClassDatabaseLoaded();

        return _manager.GetBaseField(_instance, pathId.Value);
    }

    public bool ContainsAsset(AssetPathId pathId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        return _assetPathIds.Contains(pathId.Value);
    }

    public AssetFileInfo GetAssetInfo(AssetPathId pathId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        return !ContainsAsset(pathId)
            ? throw new InvalidOperationException($"Asset not found or cannot be read: {pathId}")
            : AssetsFile.GetAssetInfo(pathId.Value);
    }

    public void SetData(AssetPathId pathId, AssetTypeValueField field)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        AssetFileInfo assetInfo = AssetsFile.GetAssetInfo(pathId.Value);
        assetInfo.SetNewData(field);
    }

    public void WriteTo(Stream outputStream)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var writer = new AssetsFileWriter(outputStream);
        AssetsFile.Write(writer);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _manager.UnloadAll(unloadClassData: true);
        _disposed = true;
    }

    private void EnsureClassDatabaseLoaded()
    {
        string unityVersion = AssetsFile.Metadata.UnityVersion;

        if (string.Equals(_loadedClassDatabaseVersion, unityVersion, StringComparison.Ordinal))
        {
            return;
        }

        _classPackageCache.LoadClassDatabase(_manager, unityVersion);

        _loadedClassDatabaseVersion = unityVersion;
    }
}

using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace UnityAssetsPatcher.AssetsTools;

public sealed class AssetsFileSession : IDisposable
{
    public AssetsFile AssetsFile => _instance.file;

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
        catch
        {
            manager.UnloadAll(unloadClassData: true);

            throw;
        }
    }

    public AssetTypeValueField GetBaseField(long pathId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        EnsureClassDatabaseLoaded();

        return _manager.GetBaseField(_instance, pathId);
    }

    public void SetData(long pathId, AssetTypeValueField field)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        AssetFileInfo assetInfo = AssetsFile.GetAssetInfo(pathId);
        assetInfo.SetNewData(field);
    }

    public void WriteTo(Stream outputStream)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var writer = new global::AssetsTools.NET.AssetsFileWriter(outputStream);
        AssetsFile.Write(writer);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _manager.UnloadAll(unloadClassData: true);
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

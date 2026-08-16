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

        try
        {
            classPackageCache.EnsureLoaded();
        }
        catch (Exception exception) when (exception is not IOException and not UnauthorizedAccessException)
        {
            throw new InvalidDataException("The AssetsTools class package could not be read.", exception);
        }

        var manager = new AssetsManager();

        try
        {
            AssetsFileInstance instance = manager.LoadAssetsFile(Path.GetFullPath(assetsFilePath), loadDeps: false);

            return new AssetsFileSession(classPackageCache, manager, instance);
        }
        catch (Exception exception)
        {
            manager.UnloadAll(unloadClassData: true);

            if (exception is IOException or UnauthorizedAccessException)
            {
                throw;
            }

            throw new InvalidDataException($"Assets file could not be read: {assetsFilePath}", exception);
        }
    }

    public AssetTypeValueField GetBaseField(AssetPathId pathId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        EnsureClassDatabaseLoaded();

        try
        {
            return _manager.GetBaseField(_instance, pathId.Value);
        }
        catch (Exception exception) when (exception is not IOException and not UnauthorizedAccessException)
        {
            throw new InvalidDataException($"Asset field could not be read: {pathId}", exception);
        }
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
        try
        {
            var writer = new AssetsFileWriter(outputStream);
            AssetsFile.Write(writer);
        }
        catch (Exception exception) when (exception is not IOException and not UnauthorizedAccessException)
        {
            throw new InvalidDataException("The assets file could not be written.", exception);
        }
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

        try
        {
            _classPackageCache.LoadClassDatabase(_manager, unityVersion);
        }
        catch (Exception exception) when (exception is not IOException and not UnauthorizedAccessException)
        {
            throw new InvalidDataException(
                $"The AssetsTools class database for Unity version '{unityVersion}' could not be loaded.",
                exception);
        }

        _loadedClassDatabaseVersion = unityVersion;
    }
}

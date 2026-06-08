using AssetsTools.NET;
using AssetsTools.NET.Extra;
using UnityAssetsPatcher.Core.Assets;

namespace UnityAssetsPatcher.AssetsTools;

internal sealed class AssetsFileSession : IDisposable
{
    private AssetsFileSession(AssetsManager manager, AssetsFileInstance assetsFileInstance)
    {
        Manager = manager;
        AssetsFileInstance = assetsFileInstance;
    }

    public AssetsManager Manager { get; }

    public AssetsFileInstance AssetsFileInstance { get; }

    public AssetsFile AssetsFile => AssetsFileInstance.file;

    public IReadOnlyList<AssetsInfo> ReadAssetsInfo()
    {
        return AssetsFile.Metadata.AssetInfos
            .Select(info => new AssetsInfo(
                info.PathId,
                info.TypeId,
                GetTypeName(info.TypeId),
                info.ByteSize))
            .ToArray();
    }

    public AssetsFieldInfo ReadAssetsFieldInfo(long pathId)
    {
        AssetTypeValueField field = Manager.GetBaseField(AssetsFileInstance, pathId);

        return field.IsDummy
            ? throw new InvalidOperationException($"Asset not found or cannot be read: {pathId}")
            : FieldTreeMapper.Map(field);
    }

    public static AssetsFileSession Open(string assetsFilePath, string tpkFilePath)
    {
        if (!File.Exists(assetsFilePath))
        {
            throw new FileNotFoundException($"Assets file not found: {assetsFilePath}", assetsFilePath);
        }

        if (!File.Exists(tpkFilePath))
        {
            throw new FileNotFoundException($"TPK file not found: {tpkFilePath}", tpkFilePath);
        }

        var manager = new AssetsManager();

        try
        {
            manager.LoadClassPackage(tpkFilePath);
            AssetsFileInstance assetsFileInstance = manager.LoadAssetsFile(assetsFilePath, true);

            manager.LoadClassDatabaseFromPackage(assetsFileInstance.file.Metadata.UnityVersion);

            return new AssetsFileSession(manager, assetsFileInstance);
        }
        catch
        {
            manager.UnloadAll(true);

            throw;
        }
    }

    public void Dispose()
    {
        Manager.UnloadAll(true);
    }

    private static string GetTypeName(int typeId)
    {
        return Enum.IsDefined(typeof(AssetClassID), typeId) ? ((AssetClassID)typeId).ToString() : "Unknown";
    }
}

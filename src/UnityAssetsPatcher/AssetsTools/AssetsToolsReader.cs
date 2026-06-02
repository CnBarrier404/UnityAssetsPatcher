using AssetsTools.NET;
using AssetsTools.NET.Extra;
using UnityAssetsPatcher.Core;

namespace UnityAssetsPatcher.AssetsTools;

public sealed class AssetsToolsReader : IAssetsReader
{
    private readonly string _tpkFilePath;

    public AssetsToolsReader(string tpkFilePath)
    {
        _tpkFilePath = tpkFilePath;
    }

    public IReadOnlyList<AssetsInfo> ReadAssetsInfo(string assetsFilePath)
    {
        if (!File.Exists(assetsFilePath))
        {
            throw new FileNotFoundException($"Assets file not found: {assetsFilePath}", assetsFilePath);
        }

        if (!File.Exists(_tpkFilePath))
        {
            throw new FileNotFoundException($"TPK file not found: {_tpkFilePath}", _tpkFilePath);
        }

        var manager = new AssetsManager();

        try
        {
            manager.LoadClassPackage(_tpkFilePath);
            AssetsFileInstance assetsFileInstance = manager.LoadAssetsFile(assetsFilePath, true);
            AssetsFile assetsFile = assetsFileInstance.file;

            // 不同 Unity 版本的序列化字段布局可能不同，必须按文件声明的版本选择类型数据库
            manager.LoadClassDatabaseFromPackage(assetsFile.Metadata.UnityVersion);

            return assetsFile.Metadata.AssetInfos
                .Select(info => new AssetsInfo(
                    info.PathId,
                    info.TypeId,
                    GetTypeName(info.TypeId),
                    info.ByteSize))
                .ToArray();
        }
        finally
        {
            // AssetsManager 会持有文件流；即使解析失败也必须释放，否则后续无法替换或恢复原文件
            manager.UnloadAll(true);
        }
    }

    private static string GetTypeName(int typeId)
    {
        return Enum.IsDefined(typeof(AssetClassID), typeId) ? ((AssetClassID)typeId).ToString() : "Unknown";
    }
}
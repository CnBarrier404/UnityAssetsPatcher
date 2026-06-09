using AssetsTools.NET;
using AssetsTools.NET.Extra;
using UnityAssetsPatcher.Core.Assets;

namespace UnityAssetsPatcher.AssetsTools;

public sealed class AssetsFileReader : IAssetsFileReader, IDisposable
{
    private readonly string _tpkFilePath;
    private readonly Dictionary<string, AssetsFileSession> _sessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IReadOnlyList<AssetsInfo>> _assetsInfo = new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, Dictionary<long, AssetsFieldInfo>> _fieldTrees =
        new(StringComparer.OrdinalIgnoreCase);

    public AssetsFileReader(string tpkFilePath)
    {
        _tpkFilePath = tpkFilePath;
    }

    public IReadOnlyList<AssetsInfo> ReadAssetsInfo(string assetsFilePath)
    {
        string fullPath = Path.GetFullPath(assetsFilePath);

        if (_assetsInfo.TryGetValue(fullPath, out var assets))
        {
            return assets;
        }

        assets = ReadSessionAssetsInfo(GetSession(fullPath));
        _assetsInfo.Add(fullPath, assets);

        return assets;
    }

    public AssetsFieldInfo ReadAssetsFieldInfo(string assetsFilePath, long pathId)
    {
        string fullPath = Path.GetFullPath(assetsFilePath);

        if (!_fieldTrees.TryGetValue(fullPath, out var assetsFileFields))
        {
            assetsFileFields = [];
            _fieldTrees.Add(fullPath, assetsFileFields);
        }

        if (assetsFileFields.TryGetValue(pathId, out AssetsFieldInfo? fieldTree))
        {
            return fieldTree;
        }

        fieldTree = ReadSessionAssetsFieldInfo(GetSession(fullPath), pathId);
        assetsFileFields.Add(pathId, fieldTree);

        return fieldTree;
    }

    public void Dispose()
    {
        foreach (AssetsFileSession session in _sessions.Values)
        {
            session.Dispose();
        }

        _sessions.Clear();
        _assetsInfo.Clear();
        _fieldTrees.Clear();
    }

    private AssetsFileSession GetSession(string fullPath)
    {
        if (_sessions.TryGetValue(fullPath, out AssetsFileSession? session))
        {
            return session;
        }

        session = AssetsFileSession.Open(fullPath, _tpkFilePath);
        _sessions.Add(fullPath, session);

        return session;
    }

    private static AssetsInfo[] ReadSessionAssetsInfo(AssetsFileSession session)
    {
        return session.AssetsFile.Metadata.AssetInfos
            .Select(info => new AssetsInfo(
                info.PathId,
                info.TypeId,
                GetTypeName(info.TypeId),
                info.ByteSize))
            .ToArray();
    }

    private static AssetsFieldInfo ReadSessionAssetsFieldInfo(AssetsFileSession session, long pathId)
    {
        AssetTypeValueField field = session.Manager.GetBaseField(session.AssetsFileInstance, pathId);

        return field.IsDummy
            ? throw new InvalidOperationException($"Asset not found or cannot be read: {pathId}")
            : AssetsFieldInfoMapper.Map(field);
    }

    private static string GetTypeName(int typeId)
    {
        return Enum.IsDefined(typeof(AssetClassID), typeId) ? ((AssetClassID)typeId).ToString() : "Unknown";
    }
}

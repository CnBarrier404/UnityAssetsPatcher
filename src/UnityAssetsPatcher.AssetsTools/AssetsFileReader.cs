using UnityAssetsPatcher.Core.Assets;

namespace UnityAssetsPatcher.AssetsTools;

public sealed class AssetsFileReader : IAssetsReader, IDisposable
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

        assets = GetSession(fullPath).ReadAssetsInfo();
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

        fieldTree = GetSession(fullPath).ReadAssetsFieldInfo(pathId);
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
}

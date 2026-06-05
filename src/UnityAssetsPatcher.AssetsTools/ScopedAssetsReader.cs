using UnityAssetsPatcher.Core.Assets;

namespace UnityAssetsPatcher.AssetsTools;

internal sealed class ScopedAssetsReader : IAssetsReadScope
{
    private readonly IAssetsFileSessionFactory _sessionFactory;
    private readonly Dictionary<string, IAssetsFileReadSession> _sessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IReadOnlyList<AssetsInfo>> _assetsInfo = new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, Dictionary<long, AssetsFieldInfo>> _fieldTrees =
        new(StringComparer.OrdinalIgnoreCase);

    public ScopedAssetsReader(IAssetsFileSessionFactory sessionFactory)
    {
        _sessionFactory = sessionFactory;
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

        if (!_fieldTrees.TryGetValue(fullPath, out Dictionary<long, AssetsFieldInfo>? assetsFileFields))
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
        foreach (IAssetsFileReadSession session in _sessions.Values)
        {
            session.Dispose();
        }

        _sessions.Clear();
        _assetsInfo.Clear();
        _fieldTrees.Clear();
    }

    private IAssetsFileReadSession GetSession(string fullPath)
    {
        if (_sessions.TryGetValue(fullPath, out IAssetsFileReadSession? session))
        {
            return session;
        }

        session = _sessionFactory.Open(fullPath);
        _sessions.Add(fullPath, session);

        return session;
    }
}

internal interface IAssetsFileSessionFactory
{
    public IAssetsFileReadSession Open(string assetsFilePath);
}

internal interface IAssetsFileReadSession : IDisposable
{
    public IReadOnlyList<AssetsInfo> ReadAssetsInfo();
    public AssetsFieldInfo ReadAssetsFieldInfo(long pathId);
}

internal sealed class AssetsFileSessionFactory : IAssetsFileSessionFactory
{
    private readonly string _tpkFilePath;

    public AssetsFileSessionFactory(string tpkFilePath)
    {
        _tpkFilePath = tpkFilePath;
    }

    public IAssetsFileReadSession Open(string assetsFilePath)
    {
        return AssetsFileSession.Open(assetsFilePath, _tpkFilePath);
    }
}

using System.Runtime.ExceptionServices;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using UnityAssetsPatcher.Core.Assets;

namespace UnityAssetsPatcher.AssetsTools;

public sealed class AssetsFileReader : IAssetsFileReader, IDisposable
{
    private readonly AssetsToolsContext _context;
    private readonly bool _ownsContext;
    private readonly Dictionary<string, AssetsFileSession> _sessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IReadOnlyList<AssetsInfo>> _assetsInfo = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    public AssetsFileReader(AssetsToolsContext context, bool ownsContext = true)
    {
        _context = context;
        _ownsContext = ownsContext;
    }

    public IReadOnlyList<AssetsInfo> ReadAssetsInfo(string assetsFilePath)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
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
        ObjectDisposedException.ThrowIf(_disposed, this);
        return ReadSessionAssetsFieldInfo(GetSession(Path.GetFullPath(assetsFilePath)), pathId);
    }

    public void CloseReadSessions()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        CloseReadSessionsCore();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        CloseReadSessionsCore();

        if (_ownsContext)
        {
            _context.Dispose();
        }
    }

    private void CloseReadSessionsCore()
    {
        ExceptionDispatchInfo? firstException = null;

        foreach (AssetsFileSession session in _sessions.Values)
        {
            try
            {
                session.Dispose();
            }
            catch (Exception exception)
            {
                firstException ??= ExceptionDispatchInfo.Capture(exception);
            }
        }

        _sessions.Clear();
        _assetsInfo.Clear();

        firstException?.Throw();
    }

    private AssetsFileSession GetSession(string fullPath)
    {
        if (_sessions.TryGetValue(fullPath, out AssetsFileSession? session))
        {
            return session;
        }

        session = AssetsFileSession.Open(fullPath, _context);
        _sessions.Add(fullPath, session);

        return session;
    }

    private static AssetsInfo[] ReadSessionAssetsInfo(AssetsFileSession session)
    {
        return session.AssetsFile.Metadata.AssetInfos
            .Select(info => new AssetsInfo(info.PathId, GetTypeName(info.TypeId)))
            .ToArray();
    }

    private static AssetsFieldInfo ReadSessionAssetsFieldInfo(AssetsFileSession session, long pathId)
    {
        AssetTypeValueField field = session.GetBaseField(pathId);

        return field.IsDummy
            ? throw new InvalidOperationException($"Asset not found or cannot be read: {pathId}")
            : AssetsFieldInfoMapper.Map(field);
    }

    private static string GetTypeName(int typeId)
    {
        return Enum.IsDefined(typeof(AssetClassID), typeId) ? ((AssetClassID)typeId).ToString() : "Unknown";
    }
}

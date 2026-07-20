using System.Runtime.ExceptionServices;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using UnityAssetsPatcher.Application.Assets;

namespace UnityAssetsPatcher.AssetsTools;

public sealed class AssetsFileReader : IAssetsFileReader
{
    private readonly AssetsToolsContext _context;
    private readonly bool _ownsContext;
    private readonly Dictionary<string, AssetsFileSession> _sessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IReadOnlyList<AssetInfo>> _assets = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    public AssetsFileReader(AssetsToolsContext context, bool ownsContext = true)
    {
        _context = context;
        _ownsContext = ownsContext;
    }

    public IReadOnlyList<AssetInfo> ReadAssets(string assetsFilePath)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        string fullPath = Path.GetFullPath(assetsFilePath);

        if (_assets.TryGetValue(fullPath, out IReadOnlyList<AssetInfo>? assets))
        {
            return assets;
        }

        assets = ReadSessionAssets(GetSession(fullPath));
        _assets.Add(fullPath, assets);

        return assets;
    }

    public AssetField ReadField(string assetsFilePath, long pathId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return ReadSessionField(GetSession(Path.GetFullPath(assetsFilePath)), pathId);
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
        _assets.Clear();

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

    private static AssetInfo[] ReadSessionAssets(AssetsFileSession session)
    {
        return session.AssetsFile.Metadata.AssetInfos
            .Select(info => new AssetInfo(info.PathId, GetTypeName(info.TypeId)))
            .ToArray();
    }

    private static AssetField ReadSessionField(AssetsFileSession session, long pathId)
    {
        AssetTypeValueField field = session.GetBaseField(pathId);

        return field.IsDummy
            ? throw new InvalidOperationException($"Asset not found or cannot be read: {pathId}")
            : AssetFieldMapper.Map(field);
    }

    private static string GetTypeName(int typeId)
    {
        return Enum.IsDefined(typeof(AssetClassID), typeId) ? ((AssetClassID)typeId).ToString() : "Unknown";
    }
}

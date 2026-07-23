using System.Runtime.ExceptionServices;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using UnityAssetsPatcher.Abstractions.Assets;
using UnityAssetsPatcher.Domain.Assets;

namespace UnityAssetsPatcher.AssetsTools;

public sealed class AssetsFileReader : IAssetsFileReader, IDisposable
{
    private static readonly IReadOnlyDictionary<int, string> TypeNames = Enum
        .GetValues<AssetClassID>()
        .Distinct()
        .ToDictionary(type => (int)type, type => Enum.GetName(type) ?? "Unknown");

    private readonly Func<Stream> _openTpkStream;
    private readonly Dictionary<string, AssetsFileSession> _sessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IReadOnlyList<AssetInfo>> _assets = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    public AssetsFileReader(Func<Stream> openTpkStream)
    {
        ArgumentNullException.ThrowIfNull(openTpkStream);
        _openTpkStream = openTpkStream;
    }

    public IReadOnlyList<AssetInfo> ReadAssets(string assetsFilePath)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        string fullPath = Path.GetFullPath(assetsFilePath);

        if (_assets.TryGetValue(fullPath, out var assets))
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

    public void CloseSessions()
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

        session = AssetsFileSession.Open(fullPath, _openTpkStream);
        _sessions.Add(fullPath, session);

        return session;
    }

    private static AssetInfo[] ReadSessionAssets(AssetsFileSession session)
    {
        return
        [
            .. session.AssetsFile.Metadata.AssetInfos
                .Select(info => new AssetInfo(info.PathId, GetTypeName(info.TypeId)))
        ];
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
        return TypeNames.GetValueOrDefault(typeId, "Unknown");
    }
}

using System.Runtime.ExceptionServices;
using UnityAssetsPatcher.Application.Assets;

namespace UnityAssetsPatcher.AssetsTools;

public sealed class AssetsToolsAccessScope : IAssetsAccessScope
{
    public IAssetsFileReader Reader { get; }
    public IAssetsFileWriter Writer { get; }

    private bool _disposed;

    public AssetsToolsAccessScope(IAssetsFileReader reader, IAssetsFileWriter writer)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(writer);

        Reader = reader;
        Writer = writer;
    }

    public void CloseReadSessions()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Reader.CloseReadSessions();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        ExceptionDispatchInfo? firstException = null;

        try
        {
            Reader.Dispose();
        }
        catch (Exception exception)
        {
            firstException = ExceptionDispatchInfo.Capture(exception);
        }

        try
        {
            Writer.Dispose();
        }
        catch (Exception exception)
        {
            firstException ??= ExceptionDispatchInfo.Capture(exception);
        }

        firstException?.Throw();
    }
}

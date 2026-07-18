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

        if (Reader is IDisposable disposableReader)
        {
            disposableReader.Dispose();
        }

        if (Writer is IDisposable disposableWriter)
        {
            disposableWriter.Dispose();
        }
    }
}

using UnityAssetsPatcher.Core.Assets;

namespace UnityAssetsPatcher.AssetsTools;

public sealed class AssetsToolsAccessScope : IAssetsAccessScope
{
    public IAssetsFileReader Reader { get; }
    public IAssetsFileWriter Writer { get; }

    private readonly IDisposable? _disposableReader;
    private bool _readResourcesReleased;

    public AssetsToolsAccessScope(IAssetsFileReader reader, IAssetsFileWriter writer)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(writer);

        Reader = reader;
        Writer = writer;
        _disposableReader = reader as IDisposable;
    }

    public void ReleaseReadResources()
    {
        if (_readResourcesReleased)
        {
            return;
        }

        _readResourcesReleased = true;
        _disposableReader?.Dispose();
    }

    public void Dispose()
    {
        ReleaseReadResources();

        if (Writer is IDisposable disposableWriter)
        {
            disposableWriter.Dispose();
        }
    }
}

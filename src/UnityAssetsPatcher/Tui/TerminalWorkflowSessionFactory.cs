using UnityAssetsPatcher.Application.Workflows;
using UnityAssetsPatcher.Core.Assets;

namespace UnityAssetsPatcher.Tui;

internal sealed class TerminalWorkflowSessionFactory : ITerminalWorkflowSessionFactory
{
    private readonly Func<IAssetsFileReader> _createAssetsReader;
    private readonly IAssetsFileWriter _assetsPatchWriter;

    public TerminalWorkflowSessionFactory(
        Func<IAssetsFileReader> createAssetsReader,
        IAssetsFileWriter assetsPatchWriter)
    {
        _createAssetsReader = createAssetsReader;
        _assetsPatchWriter = assetsPatchWriter;
    }

    public TerminalWorkflowSession CreateSession()
    {
        IAssetsFileReader assetsReader = _createAssetsReader();
        var service = new AssetsWorkflowService(assetsReader, _assetsPatchWriter);

        return new TerminalWorkflowSession(service, assetsReader as IDisposable);
    }
}

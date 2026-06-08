using UnityAssetsPatcher.Application.Workflows;
using UnityAssetsPatcher.Core.Assets;

namespace UnityAssetsPatcher.Tui;

internal sealed class TerminalWorkflowSessionFactory : ITerminalWorkflowSessionFactory
{
    private readonly Func<IAssetsReader> _createAssetsReader;
    private readonly IAssetsPatchWriter _assetsPatchWriter;

    public TerminalWorkflowSessionFactory(
        Func<IAssetsReader> createAssetsReader,
        IAssetsPatchWriter assetsPatchWriter)
    {
        _createAssetsReader = createAssetsReader;
        _assetsPatchWriter = assetsPatchWriter;
    }

    public TerminalWorkflowSession CreateSession()
    {
        IAssetsReader assetsReader = _createAssetsReader();
        var service = new AssetsWorkflowService(assetsReader, _assetsPatchWriter);

        return new TerminalWorkflowSession(service, assetsReader as IDisposable);
    }
}

using UnityAssetsPatcher.Application.Workflows;
using UnityAssetsPatcher.Core.Assets;

namespace UnityAssetsPatcher.Tui;

internal sealed class TerminalWorkflowSessionFactory : ITerminalWorkflowSessionFactory
{
    private readonly IAssetsFileService _assetsFileService;

    public TerminalWorkflowSessionFactory(IAssetsFileService assetsFileService)
    {
        _assetsFileService = assetsFileService;
    }

    public TerminalWorkflowSession CreateSession()
    {
        if (_assetsFileService is not IAssetsReadScopeFactory readScopeFactory)
        {
            return new TerminalWorkflowSession(new AssetsWorkflowService(_assetsFileService), null);
        }

        IAssetsReadScope readScope = readScopeFactory.CreateReadScope();
        var service = new AssetsWorkflowService(readScope, _assetsFileService);

        return new TerminalWorkflowSession(service, readScope);
    }
}

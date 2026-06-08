using UnityAssetsPatcher.Application.Workflows;
using UnityAssetsPatcher.Core.Assets;

namespace UnityAssetsPatcher.Tui;

internal sealed class TerminalWorkflowSessionFactory : ITerminalWorkflowSessionFactory
{
    private readonly Func<IAssetsFileReader> _createAssetsReader;
    private readonly WorkflowFactory _workflowFactory;

    public TerminalWorkflowSessionFactory(
        Func<IAssetsFileReader> createAssetsReader,
        IAssetsFileWriter assetsPatchWriter)
    {
        _createAssetsReader = createAssetsReader;
        _workflowFactory = new WorkflowFactory(assetsPatchWriter);
    }

    public TerminalWorkflowSession CreateSession()
    {
        IAssetsFileReader assetsReader = _createAssetsReader();
        InstallModWorkflow installModWorkflow = _workflowFactory.CreateInstallModWorkflow(assetsReader);

        return new TerminalWorkflowSession(installModWorkflow, assetsReader as IDisposable);
    }
}

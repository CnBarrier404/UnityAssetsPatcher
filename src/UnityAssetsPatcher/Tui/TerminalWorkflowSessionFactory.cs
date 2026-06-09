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
        InspectAssetsWorkflow inspectAssetsWorkflow = _workflowFactory.CreateInspectAssetsWorkflow(assetsReader);
        FindAssetsWorkflow findAssetsWorkflow = _workflowFactory.CreateFindAssetsWorkflow(assetsReader);

        return new TerminalWorkflowSession(
            installModWorkflow,
            inspectAssetsWorkflow,
            findAssetsWorkflow,
            assetsReader as IDisposable);
    }
}

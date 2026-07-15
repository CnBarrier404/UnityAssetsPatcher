using Terminal.Gui.ViewBase;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.TUI.Localization;

namespace UnityAssetsPatcher.TUI.Pages;

// TODO(tui-refactor): Remove this adapter when the navigator creates InspectAssetsView directly.
public sealed class InspectTerminalPage : ITerminalPage, ITerminalGUIPage
{
    public string Title => LocalizedStrings.MainMenu_InspectAssets_Title;
    public string Description => LocalizedStrings.MainMenu_InspectAssets_Description;

    private readonly IWorkflowService _workflowService;

    public InspectTerminalPage(IWorkflowService workflowService)
    {
        _workflowService = workflowService;
    }

    public View CreateView(Action returnToMainMenu)
    {
        return new InspectAssetsView(_workflowService, returnToMainMenu);
    }

    public TerminalPageResult Run()
    {
        throw new InvalidOperationException("The inspect page must run inside the Terminal.Gui shell.");
    }
}

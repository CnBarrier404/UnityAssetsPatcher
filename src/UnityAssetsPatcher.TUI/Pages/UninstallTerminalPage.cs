using Terminal.Gui.ViewBase;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.TUI.Localization;

namespace UnityAssetsPatcher.TUI.Pages;

// TODO(tui-refactor): Remove this adapter when the navigator creates UninstallModView directly.
public sealed class UninstallTerminalPage : ITerminalPage, ITerminalGUIPage
{
    public string Title => LocalizedStrings.MainMenu_UninstallMod_Title;
    public string Description => LocalizedStrings.MainMenu_UninstallMod_Description;

    private readonly IWorkflowService _workflowService;

    public UninstallTerminalPage(IWorkflowService workflowService)
    {
        _workflowService = workflowService;
    }

    public View CreateView(Action returnToMainMenu)
    {
        return new UninstallModView(_workflowService, returnToMainMenu);
    }

    public TerminalPageResult Run()
    {
        throw new InvalidOperationException("The uninstall page must run inside the Terminal.Gui shell.");
    }
}

using Terminal.Gui.ViewBase;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.TUI.Localization;

namespace UnityAssetsPatcher.TUI.Pages;

// TODO(tui-refactor): Remove this adapter when the navigator creates InstallModView directly.
public sealed class InstallTerminalPage : ITerminalPage, ITerminalGUIPage
{
    public string Title => LocalizedStrings.MainMenu_InstallMod_Title;
    public string Description => LocalizedStrings.MainMenu_InstallMod_Description;

    private readonly IWorkflowService _workflowService;
    private readonly TerminalSettings _settings;

    public InstallTerminalPage(IWorkflowService workflowService, TerminalSettings settings)
    {
        _workflowService = workflowService;
        _settings = settings;
    }

    public View CreateView(Action returnToMainMenu)
    {
        return new InstallModView(_workflowService, _settings, returnToMainMenu);
    }

    public TerminalPageResult Run()
    {
        throw new InvalidOperationException("The install page must run inside the Terminal.Gui shell.");
    }
}

using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.TUI.Localization;

namespace UnityAssetsPatcher.TUI.Pages;

internal sealed class UninstallTerminalPage : ITerminalPage
{
    public string Title => LocalizedStrings.MainMenu_UninstallMod_Title;
    public string Description => LocalizedStrings.MainMenu_UninstallMod_Description;

    private readonly IWorkflowService _workflowService;
    private readonly TerminalPageChrome _chrome;
    private readonly UninstallTerminalInput _input;
    private readonly UninstallTerminalView _view;

    public UninstallTerminalPage(
        IWorkflowService workflowService,
        TerminalPageChrome chrome,
        UninstallTerminalInput input,
        UninstallTerminalView view)
    {
        _workflowService = workflowService;
        _chrome = chrome;
        _input = input;
        _view = view;
    }

    public TerminalPageResult Run()
    {
        _chrome.ShowPage(Title, Description);

        var installed = _workflowService.ListInstalledMods();

        if (installed.Count == 0)
        {
            _view.WriteNoInstalledModsFound();

            return TerminalPageResult.ReturnToMenu();
        }

        int? selectedIndex = _input.ReadInstalledModIndex(
            installed.Count,
            (index, clear) => _view.WriteInstalledMods(Title, Description, installed, index, clear));

        if (selectedIndex is null)
        {
            return TerminalPageResult.ReturnToMenu(false);
        }

        InstallRecordSummary selected = installed[selectedIndex.Value];
        _chrome.PrepareOutputArea();
        UninstallPreviewResult preview = _workflowService.PreviewUninstall(
            new UninstallPreviewRequest(selected.InstallDirectory));

        _view.WritePreview(selected, preview);

        if (!preview.CanUninstall)
        {
            _view.WriteCannotUninstallMissingFiles();

            return TerminalPageResult.ReturnToMenu();
        }

        _view.WriteBlankLine();
        _chrome.ShowShortcutHint();

        if (!_input.ConfirmUninstall())
        {
            _view.WriteUninstallCanceled();

            return TerminalPageResult.ReturnToMenu();
        }

        _view.WriteBlankLine();
        UninstallModResult result = _workflowService.Uninstall(
            new UninstallModRequest(selected.InstallDirectory));
        _view.WriteResult(result);

        return TerminalPageResult.ReturnToMenu();
    }
}

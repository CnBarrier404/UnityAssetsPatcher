using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.TUI.Localization;

namespace UnityAssetsPatcher.TUI.Pages;

internal sealed class InstallTerminalPage : ITerminalPage
{
    public string Title => LocalizedStrings.MainMenu_InstallMod_Title;
    public string Description => LocalizedStrings.MainMenu_InstallMod_Description;

    private readonly IWorkflowService _workflowService;
    private readonly TerminalInstallOptions _installOptions;
    private readonly TerminalPageChrome _chrome;
    private readonly TerminalSettings _settings;
    private readonly InstallTerminalInput _input;
    private readonly InstallTerminalView _view;

    public InstallTerminalPage(
        IWorkflowService workflowService,
        TerminalInstallOptions installOptions,
        TerminalPageChrome chrome,
        TerminalSettings settings,
        InstallTerminalInput input,
        InstallTerminalView view)
    {
        _workflowService = workflowService;
        _installOptions = installOptions;
        _chrome = chrome;
        _settings = settings;
        _input = input;
        _view = view;
    }

    public TerminalPageResult Run()
    {
        _chrome.ShowPage(Title, Description);

        string? zipFilePath = _input.ReadModZipPath();

        if (zipFilePath is null)
        {
            return TerminalPageResult.ReturnToMenu(false);
        }

        _chrome.PrepareOutputArea();
        _view.WriteAnalyzing();

        string? gameDirectory = null;
        InstallPreviewResult? preview = TryPreviewInstall(zipFilePath, gameDirectory);

        if (preview is null)
        {
            gameDirectory = _input.ReadGameDirectory();

            if (gameDirectory is null)
            {
                return TerminalPageResult.ReturnToMenu(false);
            }

            preview = TryPreviewInstall(zipFilePath, gameDirectory);
        }

        if (preview is null)
        {
            return TerminalPageResult.ReturnToMenu();
        }

        _view.WriteInstallPreview(preview, _settings.VerboseOutput);

        _view.WriteBlankLine();
        _chrome.ShowShortcutHint();

        if (!_input.ConfirmApply())
        {
            _view.WriteInstallCanceled();

            return TerminalPageResult.ReturnToMenu();
        }

        _view.WriteBlankLine();
        InstallModResult result = _workflowService.Install(
            new InstallModRequest(zipFilePath, gameDirectory, _installOptions.BackupDirectory));
        _view.WriteInstallResult(result, _settings.VerboseOutput);

        return TerminalPageResult.ReturnToMenu();
    }

    private InstallPreviewResult? TryPreviewInstall(string zipFilePath, string? gameDirectory)
    {
        try
        {
            return _workflowService.PreviewInstall(
                new InstallPreviewRequest(zipFilePath, gameDirectory));
        }
        catch (DirectoryNotFoundException exception) when (gameDirectory is null)
        {
            _view.WriteInfo(exception.Message);
            _view.WriteBlankLine();
        }

        return null;
    }
}

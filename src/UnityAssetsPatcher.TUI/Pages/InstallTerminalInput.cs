using UnityAssetsPatcher.TUI.Localization;

namespace UnityAssetsPatcher.TUI.Pages;

internal sealed class InstallTerminalInput
{
    private readonly TerminalPrompts _prompts;

    public InstallTerminalInput(TerminalPrompts prompts)
    {
        _prompts = prompts;
    }

    public string? ReadModZipPath()
    {
        return _prompts.ReadExistingFilePath(LocalizedStrings.InstallPage_ModZipPathPrompt);
    }

    public string? ReadGameDirectory()
    {
        return _prompts.ReadExistingDirectoryPath(LocalizedStrings.InstallPage_GameDirectoryPrompt);
    }

    public bool ConfirmApply()
    {
        return _prompts.Confirm(LocalizedStrings.InstallPage_ApplyTheseChangesPrompt);
    }
}

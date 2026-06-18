using UnityAssetsPatcher.TUI.Localization;

namespace UnityAssetsPatcher.TUI.Pages;

internal sealed class UninstallTerminalInput
{
    private readonly TerminalPrompts _prompts;

    public UninstallTerminalInput(TerminalPrompts prompts)
    {
        _prompts = prompts;
    }

    public int? ReadInstalledModIndex(
        int installedModCount,
        Action<int, bool> render)
    {
        return _prompts.ReadChoiceIndex(installedModCount, 0, render);
    }

    public bool ConfirmUninstall()
    {
        return _prompts.Confirm(LocalizedStrings.UninstallPage_ConfirmPrompt);
    }
}

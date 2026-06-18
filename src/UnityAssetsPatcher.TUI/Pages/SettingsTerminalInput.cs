namespace UnityAssetsPatcher.TUI.Pages;

internal sealed class SettingsTerminalInput
{
    private readonly TerminalPrompts _prompts;

    public SettingsTerminalInput(TerminalPrompts prompts)
    {
        _prompts = prompts;
    }

    public int? ReadToggledSetting(
        int settingCount,
        int selectedIndex,
        Action<int, bool> render)
    {
        return _prompts.ReadChoiceIndex(
            settingCount,
            selectedIndex,
            render,
            acceptKey: ConsoleKey.Spacebar);
    }
}

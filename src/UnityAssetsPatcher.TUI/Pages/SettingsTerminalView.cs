using UnityAssetsPatcher.TUI.Framework;
using UnityAssetsPatcher.TUI.Localization;

namespace UnityAssetsPatcher.TUI.Pages;

internal sealed class SettingsTerminalView
{
    private readonly TerminalUI _ui;

    public SettingsTerminalView(TerminalUI ui)
    {
        _ui = ui;
    }

    public void WriteSettings(
        string title,
        IReadOnlyList<TerminalToggleDisplay> settings,
        int selectedIndex,
        bool clear)
    {
        _ui.Layout.ShowPage(
            title,
            LocalizedStrings.SettingsPage_ConfigureOutputDetailsDescription,
            shortcutHint: LocalizedStrings.SettingsPage_ShortcutHint,
            clear);
        _ui.List.WriteToggleList(settings, selectedIndex);
    }
}

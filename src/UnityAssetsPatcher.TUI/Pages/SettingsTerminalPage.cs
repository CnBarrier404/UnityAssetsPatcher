using Terminal.Gui.ViewBase;
using UnityAssetsPatcher.TUI.Localization;

namespace UnityAssetsPatcher.TUI.Pages;

// TODO(tui-refactor): Remove this adapter when the navigator creates SettingsView directly.
public sealed class SettingsTerminalPage : ITerminalPage, ITerminalGUIPage
{
    public string Title => LocalizedStrings.MainMenu_Settings_Title;
    public string Description => LocalizedStrings.MainMenu_Settings_Description;

    private readonly TerminalSettings _settings;

    public SettingsTerminalPage(TerminalSettings settings)
    {
        _settings = settings;
    }

    public View CreateView(Action returnToMainMenu)
    {
        return new SettingsView(_settings, returnToMainMenu);
    }

    public TerminalPageResult Run()
    {
        throw new InvalidOperationException("The settings page must run inside the Terminal.Gui shell.");
    }
}

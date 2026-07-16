using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using UnityAssetsPatcher.TUI.Framework;
using UnityAssetsPatcher.TUI.Localization;
using UnityAssetsPatcher.TUI.Shell;

namespace UnityAssetsPatcher.TUI.Pages;

public sealed class SettingsView : View
{
    private readonly TerminalSettings _settings;
    private readonly ToggleItem _verboseOutput;

    public SettingsView(TerminalSettings settings, Action returnToMainMenu)
    {
        _settings = settings;

        KeyDown += (_, key) =>
        {
            if (key != Key.Esc)
            {
                return;
            }

            key.Handled = true;
            returnToMainMenu();
        };

        var heading = new StyledLabel(LocalizedStrings.MainMenu_Settings_Title, TextRole.Title)
        {
            X = 0,
            Y = 0,
        };
        var description = new StyledLabel(
            LocalizedStrings.MainMenu_Settings_Description, TextRole.Muted)
        {
            X = 0,
            Y = 1,
            Width = Dim.Fill(),
        };
        _verboseOutput = new ToggleItem(
            LocalizedStrings.SettingsPage_VerboseLoggingName,
            LocalizedStrings.SettingsPage_VerboseLoggingDescription)
        {
            X = 0,
            Y = 3,
        };
        _verboseOutput.IsSelected = _settings.VerboseOutput;
        _verboseOutput.IsSelectedChanged += (_, _) => { _settings.VerboseOutput = _verboseOutput.IsSelected; };
        Add(heading, description, _verboseOutput);
    }
}

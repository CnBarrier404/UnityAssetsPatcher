using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.TUI.Framework;
using UnityAssetsPatcher.TUI.Localization;

namespace UnityAssetsPatcher.TUI.Pages;

public sealed class SettingsView : View
{
    private readonly TerminalSettings _settings;
    private readonly ToggleItem _verboseOutput;

    internal SettingsView(
        LocalizedStrings strings,
        TerminalSettings settings,
        Action returnToMainMenu,
        ILoggingLevelSwitch? loggingLevelSwitch = null)
    {
        ArgumentNullException.ThrowIfNull(strings);

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

        var heading = new StyledLabel(strings.MainMenu_Settings_Title, TextRole.Title)
        {
            X = 0,
            Y = 0,
        };
        var description = new StyledLabel(
            strings.MainMenu_Settings_Description, TextRole.Muted)
        {
            X = 0,
            Y = 1,
            Width = Dim.Fill(),
        };
        _verboseOutput = new ToggleItem(
            strings.SettingsPage_VerboseLoggingName,
            strings.SettingsPage_VerboseLoggingDescription)
        {
            X = 0,
            Y = 3,
        };
        _verboseOutput.IsSelected = _settings.VerboseOutput;
        _verboseOutput.IsSelectedChanged += (_, _) =>
        {
            _settings.VerboseOutput = _verboseOutput.IsSelected;

            if (loggingLevelSwitch is not null)
            {
                loggingLevelSwitch.MinimumLevel = _verboseOutput.IsSelected
                    ? LoggingLevel.Debug
                    : LoggingLevel.Information;
            }
        };
        Add(heading, description, _verboseOutput);
    }
}

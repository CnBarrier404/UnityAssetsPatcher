using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using UnityAssetsPatcher.TUI.Framework;
using UnityAssetsPatcher.TUI.Localization;

namespace UnityAssetsPatcher.TUI.Pages.Settings;

public sealed class SettingsView : View
{
    private readonly ToggleItem _verboseOutput;

    internal SettingsView(
        LocalizedStrings strings,
        SettingsLogic logic,
        Action returnToMainMenu)
    {
        ArgumentNullException.ThrowIfNull(strings);
        ArgumentNullException.ThrowIfNull(logic);
        ArgumentNullException.ThrowIfNull(returnToMainMenu);

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
            Y = 0
        };

        var description = new StyledLabel(strings.MainMenu_Settings_Description, TextRole.Muted)
        {
            X = 0,
            Y = 1,
            Width = Dim.Fill()
        };

        _verboseOutput = new ToggleItem(
            strings.SettingsPage_VerboseLoggingName,
            strings.SettingsPage_VerboseLoggingDescription)
        {
            X = 0,
            Y = 3,
            IsSelected = logic.VerboseLogging
        };

        _verboseOutput.IsSelectedChanged += (_, _) => logic.SetVerboseLogging(_verboseOutput.IsSelected);

        Add(heading, description, _verboseOutput);
    }
}

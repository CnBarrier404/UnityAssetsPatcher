using UnityAssetsPatcher.TUI.Framework;
using UnityAssetsPatcher.TUI.Localization;

namespace UnityAssetsPatcher.TUI.Pages.Settings;

public sealed class SettingsView : TerminalPageView
{
    private readonly ToggleItem _verboseOutput;

    internal SettingsView(
        LocalizedStrings strings,
        SettingsLogic logic)
    {
        ArgumentNullException.ThrowIfNull(strings);
        ArgumentNullException.ThrowIfNull(logic);

        SetHeader(strings.MainMenu_Settings_Title, strings.MainMenu_Settings_Description);

        _verboseOutput = new ToggleItem(
            strings.SettingsPage_VerboseLoggingName,
            strings.SettingsPage_VerboseLoggingDescription)
        {
            X = 0,
            Y = 0,
            IsSelected = logic.VerboseLogging
        };

        _verboseOutput.IsSelectedChanged += (_, _) => logic.SetVerboseLogging(_verboseOutput.IsSelected);

        Add(_verboseOutput);
    }
}

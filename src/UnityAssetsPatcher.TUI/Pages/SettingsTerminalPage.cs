using UnityAssetsPatcher.TUI.Framework;
using UnityAssetsPatcher.TUI.Localization;

namespace UnityAssetsPatcher.TUI.Pages;

internal sealed class SettingsTerminalPage : ITerminalPage
{
    public string Title => LocalizedStrings.MainMenu_Settings_Title;
    public string Description => LocalizedStrings.MainMenu_Settings_Description;

    private readonly TerminalSettings _settings;
    private readonly SettingsTerminalInput _input;
    private readonly SettingsTerminalView _view;

    public SettingsTerminalPage(
        TerminalSettings settings,
        SettingsTerminalInput input,
        SettingsTerminalView view)
    {
        _settings = settings;
        _input = input;
        _view = view;
    }

    public TerminalPageResult Run()
    {
        int selectedIndex = 0;

        while (true)
        {
            int? toggledIndex = _input.ReadToggledSetting(
                SettingsCount,
                selectedIndex,
                (index, clear) => _view.WriteSettings(Title, GetSettings(), index, clear));

            if (toggledIndex is null)
            {
                return TerminalPageResult.ReturnToMenu(false);
            }

            selectedIndex = toggledIndex.Value;
            Toggle(selectedIndex);
        }
    }

    private IReadOnlyList<TerminalToggleDisplay> GetSettings()
    {
        return
        [
            new TerminalToggleDisplay(
                LocalizedStrings.SettingsPage_VerboseLoggingName,
                LocalizedStrings.SettingsPage_VerboseLoggingDescription,
                _settings.VerboseOutput),
        ];
    }

    private void Toggle(int selectedIndex)
    {
        switch (selectedIndex)
        {
            case 0:
                _settings.VerboseOutput = !_settings.VerboseOutput;
                break;
        }
    }

    private static int SettingsCount => 1;
}

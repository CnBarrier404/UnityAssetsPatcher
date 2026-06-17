using UnityAssetsPatcher.TUI.Framework;
using UnityAssetsPatcher.TUI.Localization;

namespace UnityAssetsPatcher.TUI.Pages;

internal sealed class SettingsTerminalPage : TerminalPage
{
    public override string Title => LocalizedStrings.MainMenu_Settings_Title;
    public override string Description => LocalizedStrings.MainMenu_Settings_Description;

    public SettingsTerminalPage(TerminalAppContext context) : base(context) { }

    public override TerminalPageResult Run()
    {
        int selectedIndex = 0;

        while (true)
        {
            int? toggledIndex = Context.Prompts.ReadChoiceIndex(
                SettingsCount,
                selectedIndex,
                WriteSettings,
                acceptKey: ConsoleKey.Spacebar);

            if (toggledIndex is null)
            {
                return TerminalPageResult.ReturnToMenu(false);
            }

            selectedIndex = toggledIndex.Value;
            Toggle(selectedIndex);
        }
    }

    private void WriteSettings(int selectedIndex, bool clear)
    {
        NewPage(
            Title,
            LocalizedStrings.SettingsPage_ConfigureOutputDetailsDescription,
            LocalizedStrings.SettingsPage_ShortcutHint,
            clear);
        Context.Ui.List.WriteToggleList(GetSettings(), selectedIndex);
    }

    private IReadOnlyList<TerminalToggleDisplay> GetSettings()
    {
        return
        [
            new TerminalToggleDisplay(
                LocalizedStrings.SettingsPage_VerboseLoggingName,
                LocalizedStrings.SettingsPage_VerboseLoggingDescription,
                Context.Settings.VerboseLogging),
        ];
    }

    private void Toggle(int selectedIndex)
    {
        switch (selectedIndex)
        {
            case 0:
                Context.Settings.VerboseLogging = !Context.Settings.VerboseLogging;
                break;
        }
    }

    private static int SettingsCount => 1;
}

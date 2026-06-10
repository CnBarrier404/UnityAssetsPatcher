namespace UnityAssetsPatcher.Tui.Pages;

internal sealed class SettingsTerminalPage : TerminalPage
{
    public override string Title => "Settings";
    public override string Description => "Adjust output detail for this session.";

    public SettingsTerminalPage(TerminalAppContext context) : base(context) { }

    public override TerminalPageResult Run()
    {
        int selectedIndex = 0;
        var selectionPrompt = new TerminalSelectionPrompt(Context.Console);

        while (true)
        {
            int? toggledIndex = selectionPrompt.ReadSelection(
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
            "Configure output detail for this session.",
            "Shortcuts: ↑/↓ to choose | Space to toggle | Esc to cancel | Ctrl + C to exit",
            clear);
        TerminalOutputFormatter.WriteSettings(Context.Console, GetSettings(), selectedIndex);
    }

    private IReadOnlyList<TerminalSettingDisplay> GetSettings()
    {
        return
        [
            new TerminalSettingDisplay(
                "Verbose Logging",
                "Show detailed install preview logs, including per-asset field changes.",
                Context.Settings.VerboseLogging),
            new TerminalSettingDisplay(
                "Install timing details",
                "Show per-stage package, search, analysis, patch, and copy timings.",
                Context.Settings.InstallTimingDetails),
        ];
    }

    private void Toggle(int selectedIndex)
    {
        switch (selectedIndex)
        {
            case 0:
                Context.Settings.VerboseLogging = !Context.Settings.VerboseLogging;
                break;
            case 1:
                Context.Settings.InstallTimingDetails = !Context.Settings.InstallTimingDetails;
                break;
        }
    }

    private static int SettingsCount => 2;
}

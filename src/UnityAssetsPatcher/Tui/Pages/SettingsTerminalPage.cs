namespace UnityAssetsPatcher.Tui.Pages;

internal sealed class SettingsTerminalPage : TerminalPage
{
    public override string Title => "Settings";
    public override string Description => "Adjust output detail for this session.";

    public SettingsTerminalPage(TerminalAppContext context) : base(context) { }

    public override bool Run()
    {
        int selectedIndex = 0;
        bool clear = true;

        Context.Console.Cursor.Show(false);

        while (true)
        {
            WriteSettings(selectedIndex, clear);
            clear = false;

            var maybeKey = Context.Console.Input.ReadKey(intercept: true);

            if (maybeKey is null)
            {
                return false;
            }

            ConsoleKeyInfo key = maybeKey.Value;

            switch (key.Key)
            {
                case ConsoleKey.Escape:
                    return false;
                case ConsoleKey.UpArrow:
                    selectedIndex = selectedIndex == 0 ? SettingsCount - 1 : selectedIndex - 1;
                    break;
                case ConsoleKey.DownArrow:
                    selectedIndex = selectedIndex == SettingsCount - 1 ? 0 : selectedIndex + 1;
                    break;
                case ConsoleKey.Spacebar:
                    Toggle(selectedIndex);
                    break;
            }
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

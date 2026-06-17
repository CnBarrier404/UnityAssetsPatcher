using Spectre.Console;

namespace UnityAssetsPatcher.TUI.Framework;

public sealed class TerminalUI
{
    public TerminalLayout Layout { get; }
    public TerminalText Text { get; }
    public TerminalLists Lists { get; }
    public TerminalTables Tables { get; }
    public TerminalSummary Summary { get; }
    public TerminalStatus Status { get; }

    public TerminalUI(IAnsiConsole console)
    {
        ArgumentNullException.ThrowIfNull(console);

        Text = new TerminalText(console);
        Layout = new TerminalLayout(console, Text);
        Lists = new TerminalLists(console);
        Tables = new TerminalTables();
        Summary = new TerminalSummary(console);
        Status = new TerminalStatus(console);
    }
}

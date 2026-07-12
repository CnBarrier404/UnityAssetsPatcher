using Spectre.Console;
using UnityAssetsPatcher.Core;

namespace UnityAssetsPatcher.TUI.Framework;

public sealed class TerminalUI
{
    public TerminalLayout Layout { get; }
    public TerminalText Text { get; }
    public TerminalList List { get; }
    public TerminalSummary Summary { get; }
    public TerminalStatus Status { get; }

    public TerminalUI(IAnsiConsole console, AppInfo appInfo)
    {
        ArgumentNullException.ThrowIfNull(console);
        ArgumentNullException.ThrowIfNull(appInfo);

        Text = new TerminalText(console);
        Layout = new TerminalLayout(console, Text, appInfo);
        List = new TerminalList(console);
        Summary = new TerminalSummary(console);
        Status = new TerminalStatus(console);
    }
}

using Spectre.Console;
using UnityAssetsPatcher.Core;

namespace UnityAssetsPatcher.TUI.Framework;

public sealed class TerminalUI
{
    public TerminalLayout Layout { get; }
    public TerminalText Text { get; }
    public TerminalList List { get; }
    public TerminalTable Table { get; }
    public TerminalSummary Summary { get; }
    public TerminalStatus Status { get; }

    public TerminalUI(IAnsiConsole console) : this(console, AppInfo.Default) { }

    public TerminalUI(IAnsiConsole console, AppInfo appInfo) : this(console, appInfo, TerminalTheme.Default) { }

    private TerminalUI(IAnsiConsole console, AppInfo appInfo, TerminalTheme theme)
    {
        ArgumentNullException.ThrowIfNull(console);
        ArgumentNullException.ThrowIfNull(appInfo);
        ArgumentNullException.ThrowIfNull(theme);

        Text = new TerminalText(console);
        Layout = new TerminalLayout(console, Text, appInfo);
        List = new TerminalList(console);
        Table = new TerminalTable(console);
        Summary = new TerminalSummary(console);
        Status = new TerminalStatus(console);
    }
}

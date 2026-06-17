using Spectre.Console;

namespace UnityAssetsPatcher.TUI.Framework;

public sealed class TerminalStatus
{
    private readonly IAnsiConsole _console;

    public TerminalStatus(IAnsiConsole console)
    {
        _console = console;
    }

    public void Write(string label, string color)
    {
        _console.MarkupLine($"[bold {color}]{TerminalText.Escape(label)}[/]");
    }
}

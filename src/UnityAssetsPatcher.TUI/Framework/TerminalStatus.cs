using Spectre.Console;

namespace UnityAssetsPatcher.TUI.Framework;

public sealed class TerminalStatus
{
    private readonly IAnsiConsole _console;

    public TerminalStatus(IAnsiConsole console)
    {
        _console = console;
    }

    public void WritePreview(string label)
    {
        Write(label, TerminalTheme.StatusPreview);
    }

    public void WriteSuccess(string label)
    {
        Write(label, TerminalTheme.StatusSuccess);
    }

    private void Write(string label, string token)
    {
        _console.MarkupLine($"[{token}]{TerminalText.Escape(label)}[/]");
    }
}

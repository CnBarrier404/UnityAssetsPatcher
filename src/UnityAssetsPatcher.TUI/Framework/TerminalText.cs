using Spectre.Console;

namespace UnityAssetsPatcher.TUI.Framework;

public sealed class TerminalText
{
    private readonly IAnsiConsole _console;

    public TerminalText(IAnsiConsole console)
    {
        _console = console;
    }

    public void WriteBlankLine()
    {
        _console.Write(new Text(Environment.NewLine));
    }

    public void WriteInfo(string message)
    {
        _console.MarkupLine($"[grey]{Escape(message)}[/]");
    }

    public void WriteInputLabel(string label)
    {
        _console.Markup($"[blue]{Escape(label)}[/]: ");
    }

    public void WriteConfirmationLabel(string prompt)
    {
        _console.Markup($"[blue]{Escape(prompt)}[/] [grey]y/N[/]: ");
    }

    public void WriteError(string message)
    {
        _console.MarkupLine($"[red]{Escape(message)}[/]");
    }

    public static string Escape(string value)
    {
        return Markup.Escape(value);
    }
}

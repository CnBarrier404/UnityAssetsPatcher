using Spectre.Console;
using UnityAssetsPatcher.TUI.Localization;

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
        _console.MarkupLine($"[{TerminalTheme.Muted}]{Escape(message)}[/]");
    }

    public void WriteMarkupLine(string markup)
    {
        _console.MarkupLine(markup);
    }

    public void WriteInputLabel(string label)
    {
        _console.Markup($"[{TerminalTheme.Label}]{Escape(label)}[/]: ");
    }

    public void WriteConfirmationLabel(string prompt)
    {
        _console.Markup(
            $"[{TerminalTheme.Label}]{Escape(prompt)}[/] [{TerminalTheme.Muted}]{Escape(LocalizedStrings.Prompt_ConfirmationChoiceHint)}[/]: ");
    }

    public void WriteError(string message)
    {
        _console.MarkupLine($"[{TerminalTheme.Error}]{Escape(message)}[/]");
    }

    public static string Escape(string value)
    {
        return Markup.Escape(value);
    }
}

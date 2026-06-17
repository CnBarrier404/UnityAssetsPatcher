using System.Globalization;
using Spectre.Console;

namespace UnityAssetsPatcher.TUI.Framework;

public sealed class TerminalSummary
{
    private readonly IAnsiConsole _console;

    public TerminalSummary(IAnsiConsole console)
    {
        _console = console;
    }

    public void WriteRows(params (string Label, string Value)[] rows)
    {
        foreach ((string label, string value) in rows)
        {
            _console.MarkupLine($"[grey]{TerminalText.Escape(label),-14}[/] {TerminalText.Escape(value)}");
        }
    }

    public string FormatCount(int count, string singular)
    {
        string noun = count == 1 ? singular : $"{singular}s";

        return $"{count.ToString(CultureInfo.InvariantCulture)} {noun}";
    }

    public string FormatElapsedSeconds(TimeSpan elapsed)
    {
        return elapsed.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture);
    }
}

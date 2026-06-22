using System.Globalization;
using Spectre.Console;

namespace UnityAssetsPatcher.TUI.Framework;

public sealed class TerminalSummary
{
    private const int LabelColumnWidth = 14;

    private readonly IAnsiConsole _console;

    public TerminalSummary(IAnsiConsole console)
    {
        _console = console;
    }

    public void WriteRows(params (string Label, string Value)[] rows)
    {
        foreach ((string label, string value) in rows)
        {
            _console.MarkupLine(
                $"[{TerminalTheme.Muted}]{TerminalText.Escape(TerminalDisplay.PadRight(label, LabelColumnWidth))}[/] {TerminalText.Escape(value)}");
        }
    }

    public static string FormatCount(int count, string unit)
    {
        return $"{count.ToString(CultureInfo.InvariantCulture)} {unit}";
    }

    public static string FormatElapsedSecondsWithUnit(TimeSpan elapsed)
    {
        return $"{FormatElapsedSeconds(elapsed)} s";
    }

    private static string FormatElapsedSeconds(TimeSpan elapsed)
    {
        return elapsed.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture);
    }
}

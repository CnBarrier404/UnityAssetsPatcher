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
        var grid = new Grid();

        grid.AddColumn(new GridColumn().PadRight(2));
        grid.AddColumn();

        foreach ((string label, string value) in rows)
        {
            grid.AddRow($"[{TerminalTheme.Muted}]{TerminalText.Escape(label)}[/]", TerminalText.Escape(value));
        }

        _console.Write(grid);
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

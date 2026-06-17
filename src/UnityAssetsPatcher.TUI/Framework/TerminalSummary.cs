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
                $"[grey]{TerminalText.Escape(PadRightDisplay(label, LabelColumnWidth))}[/] {TerminalText.Escape(value)}");
        }
    }

    public string FormatCount(int count, string unit)
    {
        return $"{count.ToString(CultureInfo.InvariantCulture)} {unit}";
    }

    public string FormatElapsedSeconds(TimeSpan elapsed)
    {
        return elapsed.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture);
    }

    public string FormatElapsedSecondsWithUnit(TimeSpan elapsed)
    {
        return $"{FormatElapsedSeconds(elapsed)} s";
    }

    private static string PadRightDisplay(string value, int totalWidth)
    {
        int padding = Math.Max(totalWidth - GetDisplayWidth(value), 0);

        return value + new string(' ', padding);
    }

    private static int GetDisplayWidth(string value)
    {
        return value.Sum(character => IsWide(character) ? 2 : 1);
    }

    private static bool IsWide(char character)
    {
        return character is
            >= '\u1100' and <= '\u115f' or
            >= '\u2e80' and <= '\ua4cf' or
            >= '\uac00' and <= '\ud7a3' or
            >= '\uf900' and <= '\ufaff' or
            >= '\ufe10' and <= '\ufe19' or
            >= '\ufe30' and <= '\ufe6f' or
            >= '\uff00' and <= '\uff60' or
            >= '\uffe0' and <= '\uffe6';
    }
}

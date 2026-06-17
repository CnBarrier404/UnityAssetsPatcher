using Spectre.Console;

namespace UnityAssetsPatcher.TUI.Framework;

public readonly record struct TerminalChoiceDisplay(string Name, string Description);

public readonly record struct TerminalToggleDisplay(string Name, string Description, bool Enabled);

public sealed class TerminalList
{
    private const int DefaultOptionColumnWidth = 25;
    private const int ToggleOptionColumnWidth = 25;
    private const int ColumnGap = 1;
    private const int MinimumDescriptionColumnWidth = 10;

    private readonly IAnsiConsole _console;

    public TerminalList(IAnsiConsole console)
    {
        _console = console;
    }

    public void WriteChoiceList(IReadOnlyList<string> choices, int selectedIndex)
    {
        for (int i = 0; i < choices.Count; i++)
        {
            string indicator = i == selectedIndex ? ">" : " ";
            string line = $"{indicator} {choices[i]}";

            if (i == selectedIndex)
            {
                _console.MarkupLine($"[cyan]{TerminalText.Escape(line)}[/]");
                continue;
            }

            _console.MarkupLine(TerminalText.Escape(line));
        }
    }

    public void WriteDescribedChoiceList(
        IReadOnlyList<TerminalChoiceDisplay> choices,
        int selectedIndex,
        int? labelColumnWidth = null)
    {
        int columnWidth = labelColumnWidth ?? DefaultOptionColumnWidth;

        for (int i = 0; i < choices.Count; i++)
        {
            TerminalChoiceDisplay choice = choices[i];
            string indicator = i == selectedIndex ? ">" : " ";
            WriteDescribedLine($"{indicator} {choice.Name}", choice.Description, columnWidth, i == selectedIndex);

            if (i < choices.Count - 1)
            {
                _console.Write(new Text(Environment.NewLine));
            }
        }
    }

    public void WriteToggleList(IReadOnlyList<TerminalToggleDisplay> toggles, int selectedIndex)
    {
        for (int i = 0; i < toggles.Count; i++)
        {
            TerminalToggleDisplay toggle = toggles[i];
            string indicator = i == selectedIndex ? ">" : " ";
            string checkbox = toggle.Enabled ? "[x]" : "[ ]";
            WriteDescribedLine($"{indicator} {checkbox} {toggle.Name}", toggle.Description, ToggleOptionColumnWidth,
                i == selectedIndex);
        }
    }

    private void WriteDescribedLine(string label, string description, int labelColumnWidth, bool selected)
    {
        int consoleWidth = _console.Profile.Width <= 0 ? 120 : _console.Profile.Width;
        int columnWidth = Math.Min(labelColumnWidth,
            Math.Max(consoleWidth - MinimumDescriptionColumnWidth - ColumnGap, 1));
        int descriptionWidth = Math.Max(consoleWidth - columnWidth - ColumnGap, MinimumDescriptionColumnWidth);
        var labelLines = WrapDisplay(label, columnWidth);
        var descriptionLines = WrapDisplay(description, descriptionWidth);
        int lineCount = Math.Max(labelLines.Count, descriptionLines.Count);

        for (int i = 0; i < lineCount; i++)
        {
            string labelPart = PadRightDisplay(i < labelLines.Count ? labelLines[i] : string.Empty, columnWidth);
            string descriptionPart = i < descriptionLines.Count ? descriptionLines[i] : string.Empty;

            if (selected)
            {
                _console.MarkupLine(
                    $"[cyan]{TerminalText.Escape(labelPart)}[/] [cyan]{TerminalText.Escape(descriptionPart)}[/]");
                continue;
            }

            _console.MarkupLine(
                $"{TerminalText.Escape(labelPart)} [grey]{TerminalText.Escape(descriptionPart)}[/]");
        }
    }

    private static List<string> WrapDisplay(string value, int width)
    {
        if (string.IsNullOrEmpty(value))
        {
            return [string.Empty];
        }

        var lines = new List<string>();
        var line = new List<char>();
        int lineWidth = 0;

        foreach (char character in value)
        {
            int characterWidth = IsWide(character) ? 2 : 1;

            if (lineWidth + characterWidth > width && line.Count > 0)
            {
                lines.Add(new string(line.ToArray()).TrimEnd());
                line.Clear();
                lineWidth = 0;

                if (char.IsWhiteSpace(character))
                {
                    continue;
                }
            }

            line.Add(character);
            lineWidth += characterWidth;
        }

        if (line.Count > 0)
        {
            lines.Add(new string(line.ToArray()).TrimEnd());
        }

        return lines.Count == 0 ? [string.Empty] : lines;
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

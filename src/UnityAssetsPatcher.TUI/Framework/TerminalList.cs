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
                _console.MarkupLine($"[{TerminalTheme.Selection}]{TerminalText.Escape(line)}[/]");

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

            if (i < toggles.Count - 1)
            {
                _console.Write(new Text(Environment.NewLine));
            }
        }
    }

    private void WriteDescribedLine(string label, string description, int labelColumnWidth, bool selected)
    {
        int consoleWidth = _console.Profile.Width <= 0 ? 120 : _console.Profile.Width;
        int columnWidth = Math.Min(labelColumnWidth,
            Math.Max(consoleWidth - MinimumDescriptionColumnWidth - ColumnGap, 1));
        int descriptionWidth = Math.Max(consoleWidth - columnWidth - ColumnGap, MinimumDescriptionColumnWidth);
        IReadOnlyList<string> labelLines = TerminalDisplay.Wrap(label, columnWidth);
        IReadOnlyList<string> descriptionLines = TerminalDisplay.Wrap(description, descriptionWidth);
        int lineCount = Math.Max(labelLines.Count, descriptionLines.Count);

        for (int i = 0; i < lineCount; i++)
        {
            string labelPart = TerminalDisplay.PadRight(
                i < labelLines.Count ? labelLines[i] : string.Empty,
                columnWidth);
            string descriptionPart = i < descriptionLines.Count ? descriptionLines[i] : string.Empty;

            if (selected)
            {
                _console.MarkupLine(
                    $"[{TerminalTheme.Selection}]{TerminalText.Escape(labelPart)}[/] [{TerminalTheme.Muted}]{TerminalText.Escape(descriptionPart)}[/]");
                continue;
            }

            _console.MarkupLine(
                $"{TerminalText.Escape(labelPart)} [{TerminalTheme.Muted}]{TerminalText.Escape(descriptionPart)}[/]");
        }
    }
}

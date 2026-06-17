using Spectre.Console;

namespace UnityAssetsPatcher.TUI.Framework;

public readonly record struct TerminalChoiceDisplay(string Name, string Description);

public readonly record struct TerminalToggleDisplay(string Name, string Description, bool Enabled);

public sealed class TerminalLists
{
    private const int DefaultOptionColumnWidth = 18;
    private const int ToggleOptionColumnWidth = 35;

    private readonly IAnsiConsole _console;

    public TerminalLists(IAnsiConsole console)
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
        int labelColumnWidth = DefaultOptionColumnWidth)
    {
        for (int i = 0; i < choices.Count; i++)
        {
            TerminalChoiceDisplay choice = choices[i];
            string indicator = i == selectedIndex ? ">" : " ";
            string label = choice.Name.PadRight(labelColumnWidth);

            _console.MarkupLine(i == selectedIndex
                ? $"[cyan]{TerminalText.Escape($"{indicator} {label}")}[/] [cyan]{TerminalText.Escape(choice.Description)}[/]"
                : $"{TerminalText.Escape($"{indicator} {label}")} [grey]{TerminalText.Escape(choice.Description)}[/]");

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
            string option = $"{indicator} {checkbox} {toggle.Name}".PadRight(ToggleOptionColumnWidth);

            _console.MarkupLine(i == selectedIndex
                ? $"[cyan]{TerminalText.Escape(option)}[/] [cyan]{TerminalText.Escape(toggle.Description)}[/]"
                : $"{TerminalText.Escape(option)} [grey]{TerminalText.Escape(toggle.Description)}[/]");
        }
    }
}

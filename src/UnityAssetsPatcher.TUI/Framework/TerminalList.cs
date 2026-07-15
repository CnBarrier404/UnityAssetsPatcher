using Spectre.Console;
using System.Globalization;
using UnityAssetsPatcher.Application.Contracts;

namespace UnityAssetsPatcher.TUI.Framework;

public readonly record struct TerminalChoiceDisplay(string Name, string Description);

public readonly record struct TerminalToggleDisplay(string Name, string Description, bool Enabled);

public sealed class TerminalList
{
    private const int ColumnWidth = 35;

    private readonly IAnsiConsole _console;

    public TerminalList(IAnsiConsole console)
    {
        _console = console;
    }

    public void WriteDescribedList(
        IReadOnlyList<TerminalChoiceDisplay> choices,
        int selectedIndex,
        int? labelColumnWidth = null)
    {
        int columnWidth = labelColumnWidth ?? ColumnWidth;
        var table = new Table();

        table.Border(TableBorder.None);
        table.HideHeaders();
        table.AddColumn(new TableColumn(string.Empty).Width(columnWidth));
        table.AddColumn(new TableColumn(string.Empty));

        for (int i = 0; i < choices.Count; i++)
        {
            (string name, string descCell) = choices[i];
            char indicator = i == selectedIndex ? '>' : ' ';
            string labelCell = $"{indicator} {name}";

            table.AddRow(
                i == selectedIndex
                    ? $"[{TerminalTheme.Selection}]{TerminalText.Escape(labelCell)}[/]"
                    : TerminalText.Escape(labelCell),
                i == selectedIndex
                    ? $"[{TerminalTheme.Selection}]{TerminalText.Escape(descCell)}[/]"
                    : $"[{TerminalTheme.Muted}]{TerminalText.Escape(descCell)}[/]");

            if (i < choices.Count - 1)
            {
                table.AddRow(string.Empty, string.Empty);
            }
        }

        _console.Write(table);
    }

    public void WriteToggleList(IReadOnlyList<TerminalToggleDisplay> toggles, int selectedIndex)
    {
        var table = new Table();

        table.Border(TableBorder.None);
        table.HideHeaders();
        table.AddColumn(new TableColumn(string.Empty).Width(ColumnWidth));
        table.AddColumn(new TableColumn(string.Empty));

        for (int i = 0; i < toggles.Count; i++)
        {
            (string name, string descCell, bool enabled) = toggles[i];
            char indicator = i == selectedIndex ? '>' : ' ';
            string checkbox = enabled ? "[X]" : "[ ]";
            string labelCell = $"{indicator} {checkbox} {name}";

            table.AddRow(
                i == selectedIndex
                    ? $"[{TerminalTheme.Selection}]{TerminalText.Escape(labelCell)}[/]"
                    : TerminalText.Escape(labelCell),
                i == selectedIndex
                    ? $"[{TerminalTheme.Selection}]{TerminalText.Escape(descCell)}[/]"
                    : $"[{TerminalTheme.Muted}]{TerminalText.Escape(descCell)}[/]");

            if (i < toggles.Count - 1)
            {
                table.AddRow(string.Empty, string.Empty);
            }
        }

        _console.Write(table);
    }

    public void WriteAssets(
        IReadOnlyList<InspectAssetSummary> assets,
        string pathIdHeader,
        string typeNameHeader,
        string nameHeader)
    {
        var table = new Table();
        table.AddColumn(new TableColumn(pathIdHeader).RightAligned());
        table.AddColumn(typeNameHeader);
        table.AddColumn(nameHeader);

        foreach (InspectAssetSummary asset in assets)
        {
            table.AddRow(
                asset.PathId.ToString(CultureInfo.InvariantCulture),
                TerminalText.Escape(asset.TypeName),
                TerminalText.Escape(asset.Name ?? string.Empty));
        }

        _console.Write(table);
    }
}

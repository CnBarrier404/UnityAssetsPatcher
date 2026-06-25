using Spectre.Console;

namespace UnityAssetsPatcher.TUI.Framework;

public readonly record struct TerminalTableColumn(string Header);

public readonly record struct TerminalTableCell(string Value, string? Style = null);

public sealed class TerminalTable
{
    private readonly IAnsiConsole _console;

    public TerminalTable(IAnsiConsole console)
    {
        _console = console;
    }

    public void WritePlainTable(
        IReadOnlyList<TerminalTableColumn> columns,
        IReadOnlyList<IReadOnlyList<TerminalTableCell>> rows)
    {
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(rows);

        Table table = new Table().Border(TableBorder.None);

        foreach (TerminalTableColumn column in columns)
        {
            table.AddColumn(new TableColumn($"[{TerminalTheme.Label}]{TerminalText.Escape(column.Header)}[/]"));
        }

        foreach (var row in rows)
        {
            if (row.Count != columns.Count)
            {
                throw new ArgumentException("Every table row must have the same number of cells as the table columns.",
                    nameof(rows));
            }

            table.AddRow(row.Select(FormatCell).ToArray());
        }

        _console.Write(table);
    }

    private static string FormatCell(TerminalTableCell cell)
    {
        string value = TerminalText.Escape(cell.Value);

        return string.IsNullOrWhiteSpace(cell.Style) ? value : $"[{cell.Style}]{value}[/]";
    }
}

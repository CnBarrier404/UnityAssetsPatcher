using Terminal.Gui.Text;
using Terminal.Gui.Views;

namespace UnityAssetsPatcher.TUI.Framework;

public sealed class SummaryTableView : DataTableView
{
    public SummaryTableView(IReadOnlyList<(string Label, string Value)> rows) : base(false)
    {
        ArgumentNullException.ThrowIfNull(rows);

        const int columnGap = 3;
        int labelWidth = rows.Count == 0 ? columnGap : rows.Max(row => row.Label.GetColumns()) + columnGap;
        Width = Terminal.Gui.ViewBase.Dim.Fill();
        Height = rows.Count;
        CanFocus = false;
        Table = new SummaryTableSource(rows);
        Style.InvertSelectedCellFirstCharacter = false;
        Style.ExpandLastColumn = false;

        Style.ColumnStyles[0] = new ColumnStyle
        {
            MinWidth = labelWidth,
            MaxWidth = labelWidth,
            ColorGetter = _ => TerminalTheme.Muted
        };

        Style.ColumnStyles[1] = new ColumnStyle
        {
            ColorGetter = _ => TerminalTheme.Base
        };

        SetScheme(TerminalTheme.Base);
    }

    private sealed class SummaryTableSource(IReadOnlyList<(string Label, string Value)> rows) : ITableSource
    {
        public string[] ColumnNames => [string.Empty, string.Empty];
        public int Columns => 2;
        public int Rows => rows.Count;
        public object this[int row, int col] => col == 0 ? rows[row].Label : rows[row].Value;
    }
}

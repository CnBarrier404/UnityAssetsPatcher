using Terminal.Gui.Drawing;
using Terminal.Gui.Views;

namespace UnityAssetsPatcher.TUI.Framework;

public class DataTableView : TableView
{
    public DataTableView(bool showHeaders = true)
    {
        BorderStyle = LineStyle.None;

        Style = new TableStyle
        {
            ShowHeaders = showHeaders,
            AlwaysShowHeaders = showHeaders,
            ShowHorizontalBottomLine = false,
            ShowHorizontalHeaderOverline = false,
            ShowHorizontalHeaderUnderline = showHeaders,
            ShowVerticalCellLines = false,
            ShowVerticalCellLineForFirstColumn = false,
            ShowVerticalCellLineForLastColumn = false,
            ShowVerticalHeaderLines = false,
            ExpandLastColumn = showHeaders
        };

        SetScheme(TerminalTheme.Interactive);
    }
}

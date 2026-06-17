using Spectre.Console;

namespace UnityAssetsPatcher.TUI.Framework;

public sealed class TerminalTables
{
    public Table CreateTable()
    {
        return new Table().Border(TableBorder.Ascii).BorderColor(Color.Grey);
    }
}

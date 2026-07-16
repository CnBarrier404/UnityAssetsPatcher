using Terminal.Gui.Views;

namespace UnityAssetsPatcher.TUI.Framework;

public sealed class InputField : TextField
{
    public InputField()
    {
        SetScheme(TerminalTheme.Interactive);
    }
}

using Terminal.Gui.ViewBase;

namespace UnityAssetsPatcher.TUI;

// TODO(tui-refactor): Remove this transition interface when the navigator creates all Terminal.Gui views directly.
public interface ITerminalGUIPage
{
    public View CreateView(Action returnToMainMenu);
}

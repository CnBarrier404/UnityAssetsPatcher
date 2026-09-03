using Terminal.Gui.ViewBase;
using UnityAssetsPatcher.TUI.Navigation;

namespace UnityAssetsPatcher.TUI.Pages;

public abstract class TerminalPageView : View
{
    public event EventHandler<TerminalRoute>? NavigationRequested;

    protected void RequestNavigation(TerminalRoute route)
    {
        NavigationRequested?.Invoke(this, route);
    }
}

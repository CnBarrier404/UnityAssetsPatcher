using UnityAssetsPatcher.TUI.Navigation;

namespace UnityAssetsPatcher.TUI.Lifecycle;

public sealed class TerminalLifecycleContext
{
    public ITerminalUIDispatcher UIDispatcher { get; }
    public TerminalNavigator Navigator { get; }
    public Action RequestStop { get; }

    public TerminalLifecycleContext(
        ITerminalUIDispatcher uiDispatcher,
        TerminalNavigator navigator,
        Action requestStop)
    {
        ArgumentNullException.ThrowIfNull(uiDispatcher);
        ArgumentNullException.ThrowIfNull(navigator);
        ArgumentNullException.ThrowIfNull(requestStop);

        UIDispatcher = uiDispatcher;
        Navigator = navigator;
        RequestStop = requestStop;
    }
}

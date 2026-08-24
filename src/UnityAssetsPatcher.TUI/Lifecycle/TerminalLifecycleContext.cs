namespace UnityAssetsPatcher.TUI.Lifecycle;

public sealed class TerminalLifecycleContext
{
    public ITerminalUIDispatcher UIDispatcher { get; }
    public Action RequestStop { get; }

    public TerminalLifecycleContext(ITerminalUIDispatcher uiDispatcher, Action requestStop)
    {
        ArgumentNullException.ThrowIfNull(uiDispatcher);
        ArgumentNullException.ThrowIfNull(requestStop);

        UIDispatcher = uiDispatcher;
        RequestStop = requestStop;
    }
}

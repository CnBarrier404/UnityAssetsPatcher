using UnityAssetsPatcher.TUI.Navigation;
using UnityAssetsPatcher.TUI.Shell;

namespace UnityAssetsPatcher.TUI.Lifecycle;

public sealed class TerminalLifecycleContext
{
    public ITerminalUIDispatcher UIDispatcher { get; }
    public TerminalNavigator Navigator { get; }
    public ITerminalContentHost ContentHost { get; }
    public TerminalTaskRunner TaskRunner { get; }
    public Action RequestStop { get; }

    public TerminalLifecycleContext(
        ITerminalUIDispatcher uiDispatcher,
        TerminalNavigator navigator,
        Action requestStop)
        : this(
            uiDispatcher,
            navigator,
            navigator,
            new TerminalTaskRunner(callback => uiDispatcher.TryInvoke(callback)),
            requestStop) { }

    public TerminalLifecycleContext(
        ITerminalUIDispatcher uiDispatcher,
        TerminalNavigator navigator,
        ITerminalContentHost contentHost,
        TerminalTaskRunner taskRunner,
        Action requestStop)
    {
        ArgumentNullException.ThrowIfNull(uiDispatcher);
        ArgumentNullException.ThrowIfNull(navigator);
        ArgumentNullException.ThrowIfNull(contentHost);
        ArgumentNullException.ThrowIfNull(taskRunner);
        ArgumentNullException.ThrowIfNull(requestStop);

        UIDispatcher = uiDispatcher;
        Navigator = navigator;
        ContentHost = contentHost;
        TaskRunner = taskRunner;
        RequestStop = requestStop;
    }
}

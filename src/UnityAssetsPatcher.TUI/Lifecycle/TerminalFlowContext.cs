using UnityAssetsPatcher.TUI.Shell;

namespace UnityAssetsPatcher.TUI.Lifecycle;

internal sealed class TerminalFlowContext
{
    public ITerminalUIDispatcher UIDispatcher { get; }
    public ITerminalContentHost ContentHost { get; }
    public TerminalTaskRunner TaskRunner { get; }
    public Action RequestStop { get; }

    public TerminalFlowContext(
        ITerminalUIDispatcher uiDispatcher,
        ITerminalContentHost contentHost,
        TerminalTaskRunner taskRunner,
        Action requestStop)
    {
        ArgumentNullException.ThrowIfNull(uiDispatcher);
        ArgumentNullException.ThrowIfNull(contentHost);
        ArgumentNullException.ThrowIfNull(taskRunner);
        ArgumentNullException.ThrowIfNull(requestStop);

        UIDispatcher = uiDispatcher;
        ContentHost = contentHost;
        TaskRunner = taskRunner;
        RequestStop = requestStop;
    }
}

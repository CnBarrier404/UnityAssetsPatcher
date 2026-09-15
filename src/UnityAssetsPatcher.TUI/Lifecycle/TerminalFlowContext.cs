using UnityAssetsPatcher.TUI.Shell;

namespace UnityAssetsPatcher.TUI.Lifecycle;

internal sealed class TerminalFlowContext
{
    public ITerminalContentHost ContentHost { get; }
    public Action RequestStop { get; }
    public Func<Action, CancellationToken, Task> InvokeAsync { get; }

    public TerminalFlowContext(
        ITerminalContentHost contentHost,
        Action requestStop,
        Func<Action, CancellationToken, Task> invokeAsync)
    {
        ArgumentNullException.ThrowIfNull(contentHost);
        ArgumentNullException.ThrowIfNull(requestStop);
        ArgumentNullException.ThrowIfNull(invokeAsync);

        ContentHost = contentHost;
        RequestStop = requestStop;
        InvokeAsync = invokeAsync;
    }
}

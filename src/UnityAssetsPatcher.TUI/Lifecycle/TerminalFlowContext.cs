using UnityAssetsPatcher.TUI.Shell;

namespace UnityAssetsPatcher.TUI.Lifecycle;

internal sealed class TerminalFlowContext
{
    public ITerminalContentHost ContentHost { get; }
    public Action RequestStop { get; }

    public TerminalFlowContext(
        ITerminalContentHost contentHost,
        Action requestStop)
    {
        ArgumentNullException.ThrowIfNull(contentHost);
        ArgumentNullException.ThrowIfNull(requestStop);

        ContentHost = contentHost;
        RequestStop = requestStop;
    }
}

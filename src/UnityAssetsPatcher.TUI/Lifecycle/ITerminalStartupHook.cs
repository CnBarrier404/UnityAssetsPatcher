namespace UnityAssetsPatcher.TUI.Lifecycle;

public interface ITerminalStartupHook
{
    public Task RunAsync(TerminalLifecycleContext context, CancellationToken cancellationToken);
}

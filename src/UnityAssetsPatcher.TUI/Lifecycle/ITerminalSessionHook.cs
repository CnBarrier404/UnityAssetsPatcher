namespace UnityAssetsPatcher.TUI.Lifecycle;

public interface ITerminalSessionHook
{
    public Task RunAsync(TerminalLifecycleContext context, CancellationToken cancellationToken);
}

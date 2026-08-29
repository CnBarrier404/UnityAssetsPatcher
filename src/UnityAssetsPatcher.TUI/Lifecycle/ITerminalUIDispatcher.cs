namespace UnityAssetsPatcher.TUI.Lifecycle;

public interface ITerminalUIDispatcher
{
    public bool TryInvoke(Action callback, CancellationToken cancellationToken = default);

    public Task InvokeAsync(Action callback, CancellationToken cancellationToken = default)
    {
        return TerminalUIInvocation.InvokeAsync(this, callback, cancellationToken);
    }
}

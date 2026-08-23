namespace UnityAssetsPatcher.TUI.Lifecycle;

public interface ITerminalUIDispatcher
{
    public bool TryInvoke(Action callback, CancellationToken cancellationToken = default);
}

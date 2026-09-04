namespace UnityAssetsPatcher.TUI.Pages;

public abstract class TerminalOperationPageView : TerminalPageView
{
    protected abstract void RenderState();

    protected async Task RunLogicAsync(Func<Task> startOperation)
    {
        ArgumentNullException.ThrowIfNull(startOperation);

        Task operation = startOperation();
        RenderState();
        RequestRender();

        await operation;

        if (IsDisposed)
        {
            return;
        }

        RenderState();
        RequestRender();
    }
}

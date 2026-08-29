namespace UnityAssetsPatcher.TUI.Lifecycle;

internal static class TerminalUIInvocation
{
    public static Task InvokeAsync(
        ITerminalUIDispatcher dispatcher,
        Action callback,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(callback);

        var completion = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        bool started = dispatcher.TryInvoke(
            () =>
            {
                try
                {
                    callback();
                    completion.TrySetResult();
                }
                catch (Exception exception)
                {
                    completion.TrySetException(exception);
                }
            },
            cancellationToken);

        if (!started)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new InvalidOperationException("The terminal UI is no longer accepting updates.");
        }

        return completion.Task.WaitAsync(cancellationToken);
    }
}

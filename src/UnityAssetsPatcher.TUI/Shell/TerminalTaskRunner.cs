namespace UnityAssetsPatcher.TUI.Shell;

public sealed class TerminalTaskRunner
{
    public bool IsRunning => Volatile.Read(ref _isRunning) != 0;

    private readonly Action<Action> _dispatch;
    private int _isRunning;

    public TerminalTaskRunner(Action<Action> dispatch)
    {
        ArgumentNullException.ThrowIfNull(dispatch);
        _dispatch = dispatch;
    }

    public bool TryRun<T>(Func<T> operation, Action<T> onSucceeded, Action<Exception> onFailed)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(onSucceeded);
        ArgumentNullException.ThrowIfNull(onFailed);

        if (Interlocked.CompareExchange(ref _isRunning, 1, 0) != 0)
        {
            return false;
        }

        _ = RunAsync(() => Task.FromResult(operation()), onSucceeded, onFailed);

        return true;
    }

    public bool TryRun<T>(Func<Task<T>> operation, Action<T> onSucceeded, Action<Exception> onFailed)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(onSucceeded);
        ArgumentNullException.ThrowIfNull(onFailed);

        if (Interlocked.CompareExchange(ref _isRunning, 1, 0) != 0)
        {
            return false;
        }

        _ = RunAsync(operation, onSucceeded, onFailed);

        return true;
    }

    private async Task RunAsync<T>(Func<Task<T>> operation, Action<T> onSucceeded, Action<Exception> onFailed)
    {
        try
        {
            T result = await Task.Run(operation).ConfigureAwait(false);
            Dispatch(() => onSucceeded(result));
        }
        catch (Exception exception)
        {
            Dispatch(() => onFailed(exception));
        }
    }

    private void Dispatch(Action callback)
    {
        try
        {
            _dispatch(() =>
            {
                try
                {
                    callback();
                }
                finally
                {
                    Volatile.Write(ref _isRunning, 0);
                }
            });
        }
        catch
        {
            Volatile.Write(ref _isRunning, 0);
        }
    }
}

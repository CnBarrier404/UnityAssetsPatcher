using UnityAssetsPatcher.TUI.Lifecycle;

namespace UnityAssetsPatcher.TUI.Shell;

public sealed class TerminalTaskRunner : IAsyncDisposable
{
    private readonly object _sync = new();
    private readonly Func<Action, CancellationToken, bool> _dispatch;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly HashSet<Task> _tasks = [];
    private readonly List<Exception> _faults = [];
    private int _isAccepting = 1;
    private int _isRunning;
    private int _isDisposed;

    public TerminalTaskRunner(Action<Action> dispatch)
    {
        ArgumentNullException.ThrowIfNull(dispatch);

        _dispatch = (callback, cancellationToken) =>
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return false;
            }

            dispatch(callback);
            return true;
        };
    }

    public bool TryRun<T>(Func<Task<T>> operation, Action<T> onSucceeded, Action<Exception> onFailed)
    {
        ArgumentNullException.ThrowIfNull(operation);

        return TryRun(_ => operation(), onSucceeded, onFailed);
    }

    internal TerminalTaskRunner(ITerminalUIDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);

        _dispatch = dispatcher.TryInvoke;
    }

    public bool TryRun<T>(
        Func<CancellationToken, Task<T>> operation,
        Action<T> onSucceeded,
        Action<Exception> onFailed)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(onSucceeded);
        ArgumentNullException.ThrowIfNull(onFailed);

        if (Volatile.Read(ref _isAccepting) == 0 ||
            Interlocked.CompareExchange(ref _isRunning, 1, 0) != 0)
        {
            return false;
        }

        if (!TryStart(cancellationToken => RunAsync(operation, onSucceeded, onFailed, cancellationToken)))
        {
            Volatile.Write(ref _isRunning, 0);
            return false;
        }

        return true;
    }

    internal bool TryRunBackground(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        return TryStart(operation, cancellationToken);
    }

    public async Task StopAsync()
    {
        if (Interlocked.Exchange(ref _isAccepting, 0) != 0)
        {
            await _shutdown.CancelAsync().ConfigureAwait(false);
        }

        Task[] tasks;
        lock (_sync)
        {
            tasks = [.. _tasks];
        }

        try
        {
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch
        {
            // Faults are captured by ObserveCompletion and reported below.
        }

        Exception[] faults;
        lock (_sync)
        {
            faults = [.. _faults];
            _faults.Clear();
        }

        if (faults.Length == 1)
        {
            throw faults[0];
        }

        if (faults.Length > 1)
        {
            throw new AggregateException(faults);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
        {
            return;
        }

        try
        {
            await StopAsync().ConfigureAwait(false);
        }
        finally
        {
            _shutdown.Dispose();
        }
    }

    private bool TryStart(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        CancellationTokenSource? linkedCancellation = cancellationToken.CanBeCanceled
            ? CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token, cancellationToken)
            : null;
        CancellationToken operationCancellation = linkedCancellation?.Token ?? _shutdown.Token;
        Task task;

        lock (_sync)
        {
            if (Volatile.Read(ref _isAccepting) == 0)
            {
                linkedCancellation?.Dispose();
                return false;
            }

            task = Task.Run(() => operation(operationCancellation), CancellationToken.None);
            _tasks.Add(task);
        }

        _ = task.ContinueWith(
            completed => ObserveCompletion(completed, linkedCancellation),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

        return true;
    }

    private async Task RunAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        Action<T> onSucceeded,
        Action<Exception> onFailed,
        CancellationToken cancellationToken)
    {
        try
        {
            T result = await operation(cancellationToken).ConfigureAwait(false);
            Dispatch(() => onSucceeded(result), cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Volatile.Write(ref _isRunning, 0);
        }
        catch (Exception exception)
        {
            Dispatch(() => onFailed(exception), cancellationToken);
        }
    }

    private void Dispatch(Action callback, CancellationToken cancellationToken)
    {
        try
        {
            bool started = _dispatch(() =>
            {
                try
                {
                    callback();
                }
                finally
                {
                    Volatile.Write(ref _isRunning, 0);
                }
            }, cancellationToken);

            if (!started)
            {
                Volatile.Write(ref _isRunning, 0);
            }
        }
        catch
        {
            Volatile.Write(ref _isRunning, 0);
        }
    }

    private void ObserveCompletion(Task task, CancellationTokenSource? linkedCancellation)
    {
        lock (_sync)
        {
            _tasks.Remove(task);

            if (task.Exception is { } exception)
            {
                _faults.AddRange(exception.Flatten().InnerExceptions);
            }
        }

        linkedCancellation?.Dispose();
    }
}

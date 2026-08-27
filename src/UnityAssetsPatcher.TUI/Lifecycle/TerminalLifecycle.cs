using System.Runtime.ExceptionServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace UnityAssetsPatcher.TUI.Lifecycle;

public sealed class TerminalLifecycle
{
    private readonly IReadOnlyList<ITerminalStartupHook> _startupHooks;
    private readonly IReadOnlyList<ITerminalSessionHook> _sessionHooks;
    private readonly ILogger<TerminalLifecycle> _logger;
    private readonly Lock _gate = new();
    private readonly List<Task> _ownedTasks = [];
    private CancellationTokenSource? _sessionCancellation;
    private Task? _lifecycleTask;
    private Task? _stopTask;
    private ExceptionDispatchInfo? _fault;

    public TerminalLifecycle(
        IEnumerable<ITerminalStartupHook> startupHooks,
        IEnumerable<ITerminalSessionHook> sessionHooks,
        ILogger<TerminalLifecycle>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(startupHooks);
        ArgumentNullException.ThrowIfNull(sessionHooks);

        _startupHooks = [.. startupHooks];
        _sessionHooks = [.. sessionHooks];
        _logger = logger ?? NullLogger<TerminalLifecycle>.Instance;
    }

    public Task Start(TerminalLifecycleContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        lock (_gate)
        {
            if (_sessionCancellation is not null)
            {
                throw new InvalidOperationException("The terminal lifecycle has already started.");
            }

            _sessionCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            CancellationToken sessionToken = _sessionCancellation.Token;

            _lifecycleTask = Task.Run(() => RunLifecycleAsync(context, sessionToken), CancellationToken.None);

            _ownedTasks.Add(_lifecycleTask);

            return _lifecycleTask;
        }
    }

    public Task StopAsync()
    {
        lock (_gate)
        {
            if (_stopTask is not null)
            {
                return _stopTask;
            }

            if (_sessionCancellation is null)
            {
                return Task.CompletedTask;
            }

            _stopTask = StopCoreAsync(_sessionCancellation, [.. _ownedTasks]);

            return _stopTask;
        }
    }

    private async Task RunLifecycleAsync(TerminalLifecycleContext context, CancellationToken cancellationToken)
    {
        try
        {
            foreach (ITerminalStartupHook hook in _startupHooks)
            {
                cancellationToken.ThrowIfCancellationRequested();

                await hook.RunAsync(context, cancellationToken).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();

            await InvokeOnUiAsync(
                context,
                context.Navigator.ShowMainMenu,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            HandleFault(exception, "startup", context);

            throw;
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            Task[] sessionTasks =
                [.. _sessionHooks.Select(hook => RunSessionHookAsync(hook, context, cancellationToken))];

            await Task.WhenAll(sessionTasks).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            HandleFault(exception, "session", context);

            throw;
        }
    }

    private async Task RunSessionHookAsync(
        ITerminalSessionHook hook,
        TerminalLifecycleContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            await hook.RunAsync(context, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            HandleFault(exception, "session", context);

            throw;
        }
    }

    private static async Task InvokeOnUiAsync(
        TerminalLifecycleContext context,
        Action action,
        CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource<object?>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        bool started = context.UIDispatcher.TryInvoke(
            () =>
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                try
                {
                    action();
                    completion.TrySetResult(null);
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
            throw new InvalidOperationException("The terminal UI is no longer accepting lifecycle updates.");
        }

        await completion.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task StopCoreAsync(CancellationTokenSource sessionCancellation, IReadOnlyList<Task> ownedTasks)
    {
        try
        {
            await sessionCancellation.CancelAsync().ConfigureAwait(false);

            try
            {
                await Task.WhenAll(ownedTasks).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (sessionCancellation.IsCancellationRequested && _fault is null) { }
        }
        finally
        {
            sessionCancellation.Dispose();
        }

        _fault?.Throw();
    }

    private void HandleFault(Exception exception, string hookKind, TerminalLifecycleContext context)
    {
        ExceptionDispatchInfo fault = ExceptionDispatchInfo.Capture(exception);
        bool isFirstFault = Interlocked.CompareExchange(ref _fault, fault, null) is null;

        if (!isFirstFault)
        {
            return;
        }

        _logger.LogError(exception, "Terminal {HookKind} hook failed.", hookKind);

        CancellationTokenSource? sessionCancellation;

        lock (_gate)
        {
            sessionCancellation = _sessionCancellation;
        }

        sessionCancellation?.Cancel();

        try
        {
            context.RequestStop();
        }
        catch (Exception stopException)
        {
            _logger.LogError(stopException, "Terminal lifecycle could not request session stop.");
        }
    }
}

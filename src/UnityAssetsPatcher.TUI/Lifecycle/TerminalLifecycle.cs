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
    private Task? _startupTask;
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

            _startupTask = Task.Run(() => RunStartupHooksAsync(context, sessionToken), CancellationToken.None);

            _ownedTasks.Add(_startupTask);

            foreach (ITerminalSessionHook hook in _sessionHooks)
            {
                Task sessionTask = Task.Run(
                    () => RunSessionHookAsync(hook, context, sessionToken), CancellationToken.None);

                _ownedTasks.Add(sessionTask);
            }

            return _startupTask;
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

    private async Task RunStartupHooksAsync(TerminalLifecycleContext context, CancellationToken cancellationToken)
    {
        try
        {
            foreach (ITerminalStartupHook hook in _startupHooks)
            {
                cancellationToken.ThrowIfCancellationRequested();

                await hook.RunAsync(context, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception exception)
        {
            HandleFault(exception, "startup", context);
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
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception exception)
        {
            HandleFault(exception, "session", context);
        }
    }

    private async Task StopCoreAsync(CancellationTokenSource sessionCancellation, IReadOnlyList<Task> ownedTasks)
    {
        try
        {
            await sessionCancellation.CancelAsync().ConfigureAwait(false);
            await Task.WhenAll(ownedTasks).ConfigureAwait(false);
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

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using UnityAssetsPatcher.TUI.Flows;

namespace UnityAssetsPatcher.TUI.Lifecycle;

public sealed class TerminalLifecycle
{
    private readonly RepositoryInitializationFlow _repositoryInitialization;
    private readonly ILogger<TerminalLifecycle> _logger;

    public TerminalLifecycle(IServiceScopeFactory scopeFactory, ILogger<TerminalLifecycle>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);

        _repositoryInitialization = new RepositoryInitializationFlow(scopeFactory);
        _logger = logger ?? NullLogger<TerminalLifecycle>.Instance;
    }

    public async Task RunAsync(TerminalLifecycleContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            await _repositoryInitialization.RunAsync(context, cancellationToken).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();

            await InvokeOnUIAsync(
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
            _logger.LogError(exception, "Terminal lifecycle failed.");

            try
            {
                context.RequestStop();
            }
            catch (Exception stopException)
            {
                _logger.LogError(stopException, "Terminal lifecycle could not request session stop.");
            }

            throw;
        }
    }

    private static async Task InvokeOnUIAsync(
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
}

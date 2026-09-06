using System.Runtime.ExceptionServices;
using Microsoft.Extensions.DependencyInjection;
using UnityAssetsPatcher.Application.Operations;
using UnityAssetsPatcher.Application.Updates;

namespace UnityAssetsPatcher.TUI.Pages.MainMenu;

public sealed class MainMenuLogic : IAsyncDisposable
{
    public event EventHandler? UpdateAvailable;

    public UpdateInfo? AvailableUpdate
    {
        get
        {
            lock (_sync)
            {
                return _availableUpdate;
            }
        }
    }

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private readonly Lock _sync = new();
    private Task? _updateCheckTask;
    private UpdateInfo? _availableUpdate;
    private bool _isDisposed;

    public MainMenuLogic(IServiceScopeFactory scopeFactory)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);

        _scopeFactory = scopeFactory;
    }

    public Task StartUpdateCheck()
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);

            return _updateCheckTask ??= CheckForUpdateAsync(_lifetimeCancellation.Token);
        }
    }

    public async ValueTask DisposeAsync()
    {
        Task? updateCheckTask;

        lock (_sync)
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            updateCheckTask = _updateCheckTask;
        }

        try
        {
            Exception? cancellationFailure = null;
            try
            {
                await _lifetimeCancellation.CancelAsync().ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                cancellationFailure = exception;
            }

            try
            {
                if (updateCheckTask is not null)
                {
                    await updateCheckTask.ConfigureAwait(false);
                }
            }
            catch (Exception operationFailure) when (cancellationFailure is not null)
            {
                throw new AggregateException(operationFailure, cancellationFailure);
            }

            if (cancellationFailure is not null)
            {
                ExceptionDispatchInfo.Capture(cancellationFailure).Throw();
            }
        }
        finally
        {
            _lifetimeCancellation.Dispose();
        }
    }

    private async Task CheckForUpdateAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await DispatchUpdateCheckAsync(cancellationToken).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();

            if (result is OperationSucceeded<UpdateInfo?> { Value: { } update })
            {
                PublishAvailableUpdate(update);
            }
        }
        catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested &&
                                                           exception.CancellationToken == cancellationToken) { }
    }

    private async Task<OperationResult<UpdateInfo?>> DispatchUpdateCheckAsync(CancellationToken cancellationToken)
    {
        AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
        Exception? operationFailure = null;
        try
        {
            var updateCheckModule = scope.ServiceProvider.GetRequiredService<UpdateCheckModule>();
            return await updateCheckModule.CheckForUpdateAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            operationFailure = exception;
            throw;
        }
        finally
        {
            try
            {
                await scope.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception disposalFailure) when (operationFailure is not null)
            {
                throw new AggregateException(operationFailure, disposalFailure);
            }
        }
    }

    private void PublishAvailableUpdate(UpdateInfo update)
    {
        lock (_sync)
        {
            if (_isDisposed || _availableUpdate is not null)
            {
                return;
            }

            _availableUpdate = update;
        }

        UpdateAvailable?.Invoke(this, EventArgs.Empty);
    }
}

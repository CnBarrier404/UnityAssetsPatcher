using Microsoft.Extensions.DependencyInjection;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Features.Uninstall;
using UnityAssetsPatcher.Application.Installation;
using UnityAssetsPatcher.Application.Messaging;
using UnityAssetsPatcher.Application.Operations;

namespace UnityAssetsPatcher.TUI.Pages.UninstallMod;

public abstract record UninstallModState
{
    public sealed record LoadingInstalledMods : UninstallModState;

    public sealed record InstalledMods(IReadOnlyList<InstallRecordSummary> Records) : UninstallModState;

    public sealed record Analyzing : UninstallModState;

    public sealed record EnterGameDirectory(string InstallId, OperationError Error) : UninstallModState;

    public sealed record Preview(UninstallPreviewResult Result) : UninstallModState;

    public sealed record Uninstalling : UninstallModState;

    public sealed record Uninstalled(UninstallModResult Result) : UninstallModState;

    public sealed record Failed(OperationError Error) : UninstallModState;
}

public sealed class UninstallModLogic : IDisposable
{
    public UninstallModState State
    {
        get
        {
            lock (_sync)
            {
                return _state;
            }
        }
    }

    public bool IsWorking
    {
        get
        {
            lock (_sync)
            {
                return _isWorking;
            }
        }
    }

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private readonly Lock _sync = new();
    private UninstallModState _state = new UninstallModState.LoadingInstalledMods();
    private bool _isWorking;
    private bool _isDisposed;
    private bool _isCancellationPending;

    public UninstallModLogic(IServiceScopeFactory scopeFactory)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);

        _scopeFactory = scopeFactory;
    }

    public Task LoadInstalledModsAsync()
    {
        lock (_sync)
        {
            if (_isWorking || _isDisposed)
            {
                return Task.CompletedTask;
            }

            return StartOperation<ListInstalledModsRequest, IReadOnlyList<InstallRecordSummary>>(
                new ListInstalledModsRequest(),
                new UninstallModState.LoadingInstalledMods(),
                CompleteInstalledMods);
        }
    }

    public Task PreviewAsync(string installId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(installId);

        lock (_sync)
        {
            if (_isWorking || _isDisposed)
            {
                return Task.CompletedTask;
            }

            if (_state is not UninstallModState.InstalledMods)
            {
                throw new InvalidOperationException("An installed mod is not ready for preview.");
            }

            return StartPreview(installId, null);
        }
    }

    public Task SubmitGameDirectoryAsync(string gameDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameDirectory);

        lock (_sync)
        {
            if (_isWorking || _isDisposed)
            {
                return Task.CompletedTask;
            }

            if (_state is not UninstallModState.EnterGameDirectory state)
            {
                throw new InvalidOperationException("A game directory is not required.");
            }

            return StartPreview(state.InstallId, gameDirectory);
        }
    }

    public Task UninstallAsync()
    {
        lock (_sync)
        {
            if (_isWorking || _isDisposed)
            {
                return Task.CompletedTask;
            }

            if (_state is not UninstallModState.Preview { Result.CanUninstall: true } state)
            {
                throw new InvalidOperationException("The mod is not ready to uninstall.");
            }

            var request = new UninstallModRequest(
                state.Result.InstallId,
                state.Result.GameDirectory);

            return StartOperation<UninstallModRequest, UninstallModResult>(
                request,
                new UninstallModState.Uninstalling(),
                CompleteUninstall);
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _isCancellationPending = true;
        }

        try
        {
            _lifetimeCancellation.Cancel();
        }
        finally
        {
            lock (_sync)
            {
                _isCancellationPending = false;
                if (!_isWorking)
                {
                    _lifetimeCancellation.Dispose();
                }
            }
        }
    }

    private Task StartPreview(string installId, string? gameDirectory)
    {
        return StartOperation<UninstallPreviewRequest, UninstallPreviewResult>(
            new UninstallPreviewRequest(installId, gameDirectory),
            new UninstallModState.Analyzing(),
            result => CompletePreview(result, installId, gameDirectory));
    }

    private Task StartOperation<TRequest, TResult>(
        TRequest request,
        UninstallModState workingState,
        Action<OperationResult<TResult>> complete)
        where TRequest : IRequest<OperationResult<TResult>>
    {
        _isWorking = true;
        _state = workingState;
        CancellationToken cancellationToken = _lifetimeCancellation.Token;

        return RunOperationAsync(request, complete, cancellationToken);
    }

    private async Task RunOperationAsync<TRequest, TResult>(
        TRequest request,
        Action<OperationResult<TResult>> complete,
        CancellationToken cancellationToken)
        where TRequest : IRequest<OperationResult<TResult>>
    {
        try
        {
            var result = await DispatchOnWorkerAsync<TRequest, TResult>(
                request,
                cancellationToken).ConfigureAwait(false);

            lock (_sync)
            {
                if (!_isDisposed)
                {
                    complete(result);
                }
            }
        }
        catch (OperationCanceledException exception)
            when (cancellationToken.IsCancellationRequested &&
                  exception.CancellationToken == cancellationToken) { }
        finally
        {
            EndOperation();
        }
    }

    private void CompleteInstalledMods(
        OperationResult<IReadOnlyList<InstallRecordSummary>> result)
    {
        _state = result is OperationSucceeded<IReadOnlyList<InstallRecordSummary>> succeeded
            ? new UninstallModState.InstalledMods(succeeded.Value)
            : new UninstallModState.Failed(
                ((OperationFailed<IReadOnlyList<InstallRecordSummary>>)result).Error);
    }

    private void CompletePreview(
        OperationResult<UninstallPreviewResult> result,
        string installId,
        string? gameDirectory)
    {
        if (result is OperationSucceeded<UninstallPreviewResult> succeeded)
        {
            _state = new UninstallModState.Preview(succeeded.Value);
            return;
        }

        OperationError error = ((OperationFailed<UninstallPreviewResult>)result).Error;
        bool needsGameDirectory =
            (error.Code == GameDirectoryErrorCodes.Required ||
             error.Code == GameDirectoryErrorCodes.NotFound) &&
            gameDirectory is null;

        _state = needsGameDirectory
            ? new UninstallModState.EnterGameDirectory(installId, error)
            : new UninstallModState.Failed(error);
    }

    private void CompleteUninstall(OperationResult<UninstallModResult> result)
    {
        _state = result is OperationSucceeded<UninstallModResult> succeeded
            ? new UninstallModState.Uninstalled(succeeded.Value)
            : new UninstallModState.Failed(((OperationFailed<UninstallModResult>)result).Error);
    }

    private void EndOperation()
    {
        lock (_sync)
        {
            _isWorking = false;
            if (_isDisposed && !_isCancellationPending)
            {
                _lifetimeCancellation.Dispose();
            }
        }
    }

    private async Task<OperationResult<TResult>> DispatchOnWorkerAsync<TRequest, TResult>(
        TRequest request,
        CancellationToken cancellationToken)
        where TRequest : IRequest<OperationResult<TResult>>
    {
        return await Task.Run(async () =>
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            var dispatcher = scope.ServiceProvider.GetRequiredService<IRequestDispatcher>();
            return await dispatcher.DispatchAsync<TRequest, OperationResult<TResult>>(
                request,
                cancellationToken).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
    }
}

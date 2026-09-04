using Microsoft.Extensions.DependencyInjection;
using UnityAssetsPatcher.Application.Features.Inspect;
using UnityAssetsPatcher.Application.Messaging;
using UnityAssetsPatcher.Application.Operations;
using UnityAssetsPatcher.Domain.Assets;

namespace UnityAssetsPatcher.TUI.Pages.InspectAssets;

public enum InspectAssetsOperation
{
    ListAssets,
    ShowFields
}

public abstract record InspectAssetsState
{
    public sealed record ActionMenu : InspectAssetsState;

    public sealed record EnterListPath : InspectAssetsState;

    public sealed record SelectLimit(string AssetsFilePath) : InspectAssetsState;

    public sealed record EnterCustomLimit(string AssetsFilePath) : InspectAssetsState;

    public sealed record EnterFields : InspectAssetsState;

    public sealed record Working(InspectAssetsOperation Operation) : InspectAssetsState;

    public sealed record Assets(InspectListResult Result) : InspectAssetsState;

    public sealed record Fields(AssetField FieldTree) : InspectAssetsState;

    public sealed record Failed(InspectAssetsOperation Operation, OperationError Error) : InspectAssetsState;
}

public sealed class InspectAssetsLogic : IDisposable
{
    public InspectAssetsState State
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
    private InspectAssetsState _state = new InspectAssetsState.ActionMenu();
    private bool _isWorking;
    private bool _isDisposed;
    private bool _isCancellationPending;

    public InspectAssetsLogic(IServiceScopeFactory scopeFactory)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);

        _scopeFactory = scopeFactory;
    }

    public void ShowActionMenu()
    {
        lock (_sync)
        {
            if (_isWorking || _isDisposed)
            {
                return;
            }

            _state = new InspectAssetsState.ActionMenu();
        }
    }

    public void ShowListPathInput()
    {
        lock (_sync)
        {
            if (_isWorking || _isDisposed)
            {
                return;
            }

            if (_state is not (InspectAssetsState.ActionMenu or InspectAssetsState.SelectLimit))
            {
                throw new InvalidOperationException("The list path input is not available.");
            }

            _state = new InspectAssetsState.EnterListPath();
        }
    }

    public void SubmitListPath(string assetsFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetsFilePath);

        lock (_sync)
        {
            if (_isWorking || _isDisposed)
            {
                return;
            }

            if (_state is not InspectAssetsState.EnterListPath)
            {
                throw new InvalidOperationException("An assets file path is not expected.");
            }

            _state = new InspectAssetsState.SelectLimit(assetsFilePath);
        }
    }

    public void ShowCustomLimitInput()
    {
        lock (_sync)
        {
            if (_isWorking || _isDisposed)
            {
                return;
            }

            if (_state is not InspectAssetsState.SelectLimit state)
            {
                throw new InvalidOperationException("A custom limit is not available.");
            }

            _state = new InspectAssetsState.EnterCustomLimit(state.AssetsFilePath);
        }
    }

    public void ReturnToLimitChoices()
    {
        lock (_sync)
        {
            if (_isWorking || _isDisposed)
            {
                return;
            }

            if (_state is not InspectAssetsState.EnterCustomLimit state)
            {
                throw new InvalidOperationException("The limit choices are not available.");
            }

            _state = new InspectAssetsState.SelectLimit(state.AssetsFilePath);
        }
    }

    public void ShowFieldsInput()
    {
        lock (_sync)
        {
            if (_isWorking || _isDisposed)
            {
                return;
            }

            if (_state is not InspectAssetsState.ActionMenu)
            {
                throw new InvalidOperationException("The fields input is not available.");
            }

            _state = new InspectAssetsState.EnterFields();
        }
    }

    public Task InspectListAsync(int? limit)
    {
        if (limit is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }

        lock (_sync)
        {
            if (_isWorking || _isDisposed)
            {
                return Task.CompletedTask;
            }

            string assetsFilePath = _state switch
            {
                InspectAssetsState.SelectLimit state => state.AssetsFilePath,
                InspectAssetsState.EnterCustomLimit state => state.AssetsFilePath,
                _ => throw new InvalidOperationException("An assets list is not ready for inspection.")
            };

            return StartOperation<InspectListRequest, InspectListResult>(
                new InspectListRequest(assetsFilePath, limit),
                InspectAssetsOperation.ListAssets,
                CompleteList);
        }
    }

    public Task InspectFieldsAsync(string assetsFilePath, long pathId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetsFilePath);

        lock (_sync)
        {
            if (_isWorking || _isDisposed)
            {
                return Task.CompletedTask;
            }

            if (_state is not InspectAssetsState.EnterFields)
            {
                throw new InvalidOperationException("Asset fields are not ready for inspection.");
            }

            return StartOperation<InspectFieldsRequest, AssetField>(
                new InspectFieldsRequest(assetsFilePath, pathId),
                InspectAssetsOperation.ShowFields,
                CompleteFields);
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

    private Task StartOperation<TRequest, TResult>(
        TRequest request,
        InspectAssetsOperation operation,
        Action<OperationResult<TResult>> complete)
        where TRequest : IRequest<OperationResult<TResult>>
    {
        _isWorking = true;
        _state = new InspectAssetsState.Working(operation);
        CancellationToken cancellationToken = _lifetimeCancellation.Token;

        return RunOperationAsync(request, operation, complete, cancellationToken);
    }

    private async Task RunOperationAsync<TRequest, TResult>(
        TRequest request,
        InspectAssetsOperation operation,
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
                if (_isDisposed)
                {
                    return;
                }

                if (result is OperationFailed<TResult> failed)
                {
                    _state = new InspectAssetsState.Failed(operation, failed.Error);
                    return;
                }

                complete(result);
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

    private void CompleteList(OperationResult<InspectListResult> result)
    {
        var succeeded = (OperationSucceeded<InspectListResult>)result;
        _state = new InspectAssetsState.Assets(succeeded.Value);
    }

    private void CompleteFields(OperationResult<AssetField> result)
    {
        var succeeded = (OperationSucceeded<AssetField>)result;
        _state = new InspectAssetsState.Fields(succeeded.Value);
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

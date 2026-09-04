using Microsoft.Extensions.DependencyInjection;
using UnityAssetsPatcher.Application.Messaging;
using UnityAssetsPatcher.Application.Operations;

namespace UnityAssetsPatcher.TUI.Pages;

public abstract class TerminalPageLogic<TState> : IDisposable
    where TState : notnull
{
    public TState State
    {
        get
        {
            lock (SyncRoot)
            {
                return CurrentState;
            }
        }
    }

    public bool IsWorking
    {
        get
        {
            lock (SyncRoot)
            {
                return _isWorking;
            }
        }
    }

    protected Lock SyncRoot { get; } = new();
    protected TState CurrentState { get; set; }
    protected bool IsUnavailable => _isWorking || IsDisposed;

    protected bool IsDisposed { get; private set; }

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly CancellationTokenSource _lifetimeCancellation;
    private bool _isWorking;
    private bool _isCancellationPending;

    protected TerminalPageLogic(
        IServiceScopeFactory scopeFactory,
        TState initialState,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(initialState);

        _scopeFactory = scopeFactory;
        CurrentState = initialState;
        _lifetimeCancellation = cancellationToken.CanBeCanceled
            ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
            : new CancellationTokenSource();
    }

    public void Dispose()
    {
        lock (SyncRoot)
        {
            if (IsDisposed)
            {
                return;
            }

            IsDisposed = true;
            _isCancellationPending = true;
        }

        try
        {
            _lifetimeCancellation.Cancel();
        }
        finally
        {
            lock (SyncRoot)
            {
                _isCancellationPending = false;
                if (!_isWorking)
                {
                    _lifetimeCancellation.Dispose();
                }
            }
        }
    }

    protected Task StartOperation<TRequest, TResult>(
        TRequest request,
        Action<OperationResult<TResult>> complete)
        where TRequest : IRequest<OperationResult<TResult>>
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(complete);

        CancellationToken cancellationToken;
        lock (SyncRoot)
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            if (_isWorking)
            {
                throw new InvalidOperationException("A page operation is already running.");
            }

            _isWorking = true;
            cancellationToken = _lifetimeCancellation.Token;
        }

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

            lock (SyncRoot)
            {
                if (!IsDisposed)
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

    private void EndOperation()
    {
        lock (SyncRoot)
        {
            _isWorking = false;
            if (IsDisposed && !_isCancellationPending)
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

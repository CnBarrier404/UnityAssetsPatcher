using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Features.Recovery;
using UnityAssetsPatcher.Application.Features.RepositoryManagement;
using UnityAssetsPatcher.Application.Messaging;
using UnityAssetsPatcher.Application.Operations;
using UnityAssetsPatcher.Application.Repository;

namespace UnityAssetsPatcher.TUI.Pages.RepositoryIssue;

public abstract record RepositoryIssueState
{
    public sealed record Unchecked : RepositoryIssueState;

    public sealed record Ready : RepositoryIssueState;

    public sealed record UnsupportedFormat(
        string ActualVersion,
        string SupportedVersion,
        OperationError? ClearError = null) : RepositoryIssueState;

    public sealed record ClearConfirmation(UnsupportedFormat Unsupported) : RepositoryIssueState;

    public sealed record RecoveryProblem(RepositoryRecoveryReport Report) : RepositoryIssueState;

    public sealed record RecoveryPreview(
        RepositoryRecoveryPreview Preview,
        RepositoryRecoveryReport PreviousReport) : RepositoryIssueState;
}

public sealed class RepositoryIssueLogic : IDisposable
{
    public RepositoryIssueState State
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

    public Task Completion => _completion.Task;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly CancellationTokenSource _lifetimeCancellation;

    private readonly TaskCompletionSource _completion = new(
        TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly Lock _sync = new();
    private RepositoryIssueState _state = new RepositoryIssueState.Unchecked();
    private bool _isWorking;
    private bool _isDisposed;
    private bool _isCancellationPending;

    public RepositoryIssueLogic(
        IServiceScopeFactory scopeFactory,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);

        _scopeFactory = scopeFactory;
        _lifetimeCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    }

    public Task InitializeAsync()
    {
        lock (_sync)
        {
            if (_isWorking || _isDisposed)
            {
                return Task.CompletedTask;
            }

            return StartOperation<InitializeRepositoryRequest, RepositoryRecoveryReport>(
                new InitializeRepositoryRequest(),
                CompleteInitialization);
        }
    }

    public void ShowClearConfirmation()
    {
        lock (_sync)
        {
            ThrowIfUnavailable();

            if (_state is not RepositoryIssueState.UnsupportedFormat unsupported)
            {
                throw new InvalidOperationException("The repository format warning is not active.");
            }

            _state = new RepositoryIssueState.ClearConfirmation(unsupported);
        }
    }

    public void CancelClearConfirmation()
    {
        lock (_sync)
        {
            ThrowIfUnavailable();

            if (_state is not RepositoryIssueState.ClearConfirmation confirmation)
            {
                throw new InvalidOperationException("The repository clear confirmation is not active.");
            }

            _state = confirmation.Unsupported;
        }
    }

    public Task ClearAsync()
    {
        lock (_sync)
        {
            if (_isWorking || _isDisposed)
            {
                return Task.CompletedTask;
            }

            if (_state is not RepositoryIssueState.ClearConfirmation confirmation)
            {
                throw new InvalidOperationException("The repository clear operation has not been confirmed.");
            }

            RepositoryIssueState.UnsupportedFormat unsupported = confirmation.Unsupported;

            return StartOperation<ClearUnsupportedRepositoryRequest, RepositoryClearResult>(
                new ClearUnsupportedRepositoryRequest(),
                result => CompleteClear(result, unsupported));
        }
    }

    public Task PreviewRecoveryAsync(string gameDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameDirectory);

        lock (_sync)
        {
            if (_isWorking || _isDisposed)
            {
                return Task.CompletedTask;
            }

            if (_state is not RepositoryIssueState.RecoveryProblem
                {
                    Report.Status: RepositoryRecoveryStatus.RecoveryRequired
                } recovery)
            {
                throw new InvalidOperationException("The repository is not waiting for a recovery preview.");
            }

            return StartOperation<PreviewRecoveryRequest, RepositoryRecoveryPreview>(
                new PreviewRecoveryRequest(gameDirectory),
                result => CompleteRecoveryPreview(result, recovery.Report));
        }
    }

    public void BackToRecovery()
    {
        lock (_sync)
        {
            ThrowIfUnavailable();

            if (_state is not RepositoryIssueState.RecoveryPreview preview)
            {
                throw new InvalidOperationException("The repository recovery preview is not active.");
            }

            _state = new RepositoryIssueState.RecoveryProblem(preview.PreviousReport);
        }
    }

    public Task RecoverAsync()
    {
        lock (_sync)
        {
            if (_isWorking || _isDisposed)
            {
                return Task.CompletedTask;
            }

            if (_state is not RepositoryIssueState.RecoveryPreview
                {
                    Preview.GameDirectory: { } gameDirectory
                })
            {
                throw new InvalidOperationException("The repository recovery has not been previewed.");
            }

            return StartOperation<RecoverRecoveryRequest, RepositoryRecoveryReport>(
                new RecoverRecoveryRequest(gameDirectory),
                CompleteRecovery);
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
        Action<OperationResult<TResult>> complete)
        where TRequest : IRequest<OperationResult<TResult>>
    {
        _isWorking = true;
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

    private void CompleteInitialization(OperationResult<RepositoryRecoveryReport> result)
    {
        if (result is OperationFailed<RepositoryRecoveryReport> failed &&
            failed.Error.Code == RepositoryErrorCodes.UnsupportedVersion)
        {
            _state = CreateUnsupportedState(failed.Error);

            return;
        }

        RepositoryRecoveryReport recovery = result switch
        {
            OperationSucceeded<RepositoryRecoveryReport> succeeded => succeeded.Value,
            OperationFailed<RepositoryRecoveryReport> recoveryFailed =>
                recoveryFailed.Error.Recovery ?? FailedRecovery(),
            _ => throw new ArgumentOutOfRangeException(nameof(result))
        };

        SetRecoveryState(recovery);
    }

    private void CompleteClear(
        OperationResult<RepositoryClearResult> result,
        RepositoryIssueState.UnsupportedFormat unsupported)
    {
        if (result is OperationSucceeded<RepositoryClearResult>)
        {
            SetReady();

            return;
        }

        var failed = result as OperationFailed<RepositoryClearResult> ??
                     throw new ArgumentOutOfRangeException(nameof(result));
        _state = unsupported with { ClearError = failed.Error };
    }

    private void CompleteRecoveryPreview(
        OperationResult<RepositoryRecoveryPreview> result,
        RepositoryRecoveryReport previousReport)
    {
        if (result is not OperationSucceeded<RepositoryRecoveryPreview> succeeded)
        {
            SetRecoveryState(FailedRecovery());

            return;
        }

        RepositoryRecoveryPreview preview = succeeded.Value;
        if (preview.CanRecover)
        {
            _state = new RepositoryIssueState.RecoveryPreview(preview, previousReport);

            return;
        }

        SetRecoveryState(new RepositoryRecoveryReport(preview.Status, [], preview.Issues));
    }

    private void CompleteRecovery(OperationResult<RepositoryRecoveryReport> result)
    {
        RepositoryRecoveryReport recovery = result switch
        {
            OperationSucceeded<RepositoryRecoveryReport> succeeded => succeeded.Value,
            OperationFailed<RepositoryRecoveryReport> failed =>
                failed.Error.Recovery ?? FailedRecovery(),
            _ => throw new ArgumentOutOfRangeException(nameof(result))
        };

        SetRecoveryState(recovery);
    }

    private void SetRecoveryState(RepositoryRecoveryReport recovery)
    {
        if (recovery.Status is RepositoryRecoveryStatus.RecoveryRequired or RepositoryRecoveryStatus.Locked)
        {
            _state = new RepositoryIssueState.RecoveryProblem(recovery);

            return;
        }

        SetReady();
    }

    private void SetReady()
    {
        _state = new RepositoryIssueState.Ready();
        _completion.TrySetResult();
    }

    private static RepositoryIssueState.UnsupportedFormat CreateUnsupportedState(OperationError error)
    {
        string actualVersion = ParameterText(error, "actual") ?? "?";
        string supportedVersion = ParameterText(error, "supported") ??
                                  RepositoryService.CurrentRepositoryFormatVersion.ToString(
                                      CultureInfo.InvariantCulture);

        return new RepositoryIssueState.UnsupportedFormat(actualVersion, supportedVersion);
    }

    private static RepositoryRecoveryReport FailedRecovery()
    {
        return new RepositoryRecoveryReport(
            RepositoryRecoveryStatus.Locked,
            [],
            [new RepositoryRecoveryIssue(RepositoryRecoveryIssueCode.UnexpectedFailure, string.Empty)]);
    }

    private static string? ParameterText(OperationError error, string key)
    {
        return error.Parameters.TryGetValue(key, out object? value)
            ? Convert.ToString(value, CultureInfo.InvariantCulture)
            : null;
    }

    private void ThrowIfUnavailable()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        if (_isWorking)
        {
            throw new InvalidOperationException("A repository operation is already running.");
        }
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

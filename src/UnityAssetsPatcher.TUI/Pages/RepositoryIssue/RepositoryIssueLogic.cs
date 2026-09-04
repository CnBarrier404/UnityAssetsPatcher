using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Features.Recovery;
using UnityAssetsPatcher.Application.Features.RepositoryManagement;
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

public sealed class RepositoryIssueLogic : TerminalPageLogic<RepositoryIssueState>
{
    public Task Completion => _completion.Task;

    private readonly TaskCompletionSource _completion = new(
        TaskCreationOptions.RunContinuationsAsynchronously);

    public RepositoryIssueLogic(
        IServiceScopeFactory scopeFactory,
        CancellationToken cancellationToken)
        : base(scopeFactory, new RepositoryIssueState.Unchecked(), cancellationToken) { }

    public Task InitializeAsync()
    {
        lock (SyncRoot)
        {
            if (IsUnavailable)
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
        lock (SyncRoot)
        {
            ThrowIfUnavailable();

            if (CurrentState is not RepositoryIssueState.UnsupportedFormat unsupported)
            {
                throw new InvalidOperationException("The repository format warning is not active.");
            }

            CurrentState = new RepositoryIssueState.ClearConfirmation(unsupported);
        }
    }

    public void CancelClearConfirmation()
    {
        lock (SyncRoot)
        {
            ThrowIfUnavailable();

            if (CurrentState is not RepositoryIssueState.ClearConfirmation confirmation)
            {
                throw new InvalidOperationException("The repository clear confirmation is not active.");
            }

            CurrentState = confirmation.Unsupported;
        }
    }

    public Task ClearAsync()
    {
        lock (SyncRoot)
        {
            if (IsUnavailable)
            {
                return Task.CompletedTask;
            }

            if (CurrentState is not RepositoryIssueState.ClearConfirmation confirmation)
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

        lock (SyncRoot)
        {
            if (IsUnavailable)
            {
                return Task.CompletedTask;
            }

            if (CurrentState is not RepositoryIssueState.RecoveryProblem
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
        lock (SyncRoot)
        {
            ThrowIfUnavailable();

            if (CurrentState is not RepositoryIssueState.RecoveryPreview preview)
            {
                throw new InvalidOperationException("The repository recovery preview is not active.");
            }

            CurrentState = new RepositoryIssueState.RecoveryProblem(preview.PreviousReport);
        }
    }

    public Task RecoverAsync()
    {
        lock (SyncRoot)
        {
            if (IsUnavailable)
            {
                return Task.CompletedTask;
            }

            if (CurrentState is not RepositoryIssueState.RecoveryPreview
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

    private void CompleteInitialization(OperationResult<RepositoryRecoveryReport> result)
    {
        if (result is OperationFailed<RepositoryRecoveryReport> failed &&
            failed.Error.Code == RepositoryErrorCodes.UnsupportedVersion)
        {
            CurrentState = CreateUnsupportedState(failed.Error);

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
        CurrentState = unsupported with { ClearError = failed.Error };
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
            CurrentState = new RepositoryIssueState.RecoveryPreview(preview, previousReport);

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
            CurrentState = new RepositoryIssueState.RecoveryProblem(recovery);

            return;
        }

        SetReady();
    }

    private void SetReady()
    {
        CurrentState = new RepositoryIssueState.Ready();
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
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        if (IsWorking)
        {
            throw new InvalidOperationException("A repository operation is already running.");
        }
    }
}

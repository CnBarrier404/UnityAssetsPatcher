using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Features.Recovery;
using UnityAssetsPatcher.Application.Features.RepositoryManagement;
using UnityAssetsPatcher.Application.Messaging;
using UnityAssetsPatcher.Application.Operations;
using UnityAssetsPatcher.Application.Repository;
using UnityAssetsPatcher.TUI.Lifecycle;
using UnityAssetsPatcher.TUI.Localization;
using UnityAssetsPatcher.TUI.Pages;

namespace UnityAssetsPatcher.TUI.Flows;

internal sealed class RepositoryInitializationFlow
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly LocalizedStrings _strings;
    private RepositoryRecoveryReport _recovery = RepositoryRecoveryReport.Clean;
    private bool _isWorking;

    public RepositoryInitializationFlow(IServiceScopeFactory scopeFactory)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);

        _scopeFactory = scopeFactory;
        _strings = new LocalizedStrings(CultureInfo.CurrentUICulture);
    }

    public async Task RunAsync(TerminalFlowContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var completion = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var result = await Task.Run(
            () => InitializeRepository(cancellationToken),
            cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();
        ShowInitializationResult(context, result, completion, cancellationToken);

        await completion.Task.WaitAsync(cancellationToken);
    }

    private OperationResult<RepositoryRecoveryReport> InitializeRepository(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using IServiceScope scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRepository>();

        return new RepositoryInitializationModule(repository).Initialize();
    }

    private void ShowInitializationResult(
        TerminalFlowContext context,
        OperationResult<RepositoryRecoveryReport> result,
        TaskCompletionSource completion,
        CancellationToken cancellationToken)
    {
        if (result is OperationFailed<RepositoryRecoveryReport> failed &&
            failed.Error.Code == RepositoryErrorCodes.UnsupportedVersion)
        {
            ShowUnsupportedRepository(context, failed.Error, completion, cancellationToken);

            return;
        }

        _recovery = result switch
        {
            OperationSucceeded<RepositoryRecoveryReport> succeeded => succeeded.Value,
            OperationFailed<RepositoryRecoveryReport> recoveryFailed =>
                recoveryFailed.Error.Recovery ?? FailedRecovery(),
            _ => throw new ArgumentOutOfRangeException(nameof(result))
        };

        ShowRecoveryResult(context, completion, cancellationToken);
    }

    private async Task PreviewRecoveryAsync(
        TerminalFlowContext context,
        string gameDirectory,
        TaskCompletionSource completion,
        CancellationToken cancellationToken)
    {
        if (_isWorking)
        {
            return;
        }

        _isWorking = true;

        try
        {
            var result = await DispatchAsync<
                PreviewRecoveryRequest,
                OperationResult<RepositoryRecoveryPreview>>(
                new PreviewRecoveryRequest(gameDirectory),
                cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            if (result is not OperationSucceeded<RepositoryRecoveryPreview> succeeded)
            {
                ShowRecoveryFailure(context, completion, cancellationToken);

                return;
            }

            RepositoryRecoveryPreview preview = succeeded.Value;

            if (!preview.CanRecover)
            {
                _recovery = new RepositoryRecoveryReport(preview.Status, [], preview.Issues);
                ShowRecoveryResult(context, completion, cancellationToken);

                return;
            }

            context.ContentHost.ShowContent(new RepositoryRecoveryPreviewView(
                _strings,
                preview,
                () => RecoverAsync(context, preview.GameDirectory!, completion, cancellationToken),
                () => ShowRecoveryResult(context, completion, cancellationToken),
                context.RequestStop));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception exception)
        {
            SetOperationFailure(completion, cancellationToken, exception);
        }
        finally
        {
            _isWorking = false;
        }
    }

    private async Task RetryRepositoryInitializationAsync(
        TerminalFlowContext context,
        TaskCompletionSource completion,
        CancellationToken cancellationToken)
    {
        if (_isWorking)
        {
            return;
        }

        _isWorking = true;

        try
        {
            var result = await Task.Run(
                () => InitializeRepository(cancellationToken),
                cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();
            ShowInitializationResult(context, result, completion, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception exception)
        {
            SetOperationFailure(completion, cancellationToken, exception);
        }
        finally
        {
            _isWorking = false;
        }
    }

    private async Task RecoverAsync(
        TerminalFlowContext context,
        string gameDirectory,
        TaskCompletionSource completion,
        CancellationToken cancellationToken)
    {
        if (_isWorking)
        {
            return;
        }

        _isWorking = true;

        try
        {
            var result = await DispatchAsync<
                RecoverRecoveryRequest,
                OperationResult<RepositoryRecoveryReport>>(
                new RecoverRecoveryRequest(gameDirectory),
                cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            _recovery = result switch
            {
                OperationSucceeded<RepositoryRecoveryReport> succeeded => succeeded.Value,
                OperationFailed<RepositoryRecoveryReport> failed =>
                    failed.Error.Recovery ?? FailedRecovery(),
                _ => throw new ArgumentOutOfRangeException(nameof(result))
            };

            ShowRecoveryResult(context, completion, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception exception)
        {
            SetOperationFailure(completion, cancellationToken, exception);
        }
        finally
        {
            _isWorking = false;
        }
    }

    private void ShowUnsupportedRepository(
        TerminalFlowContext context,
        OperationError formatError,
        TaskCompletionSource completion,
        CancellationToken cancellationToken,
        OperationError? clearError = null)
    {
        string actualVersion = ParameterText(formatError, "actual") ?? "?";
        string supportedVersion = ParameterText(formatError, "supported") ??
                                  RepositoryService.CurrentRepositoryFormatVersion.ToString(
                                      CultureInfo.InvariantCulture);
        string? failure = clearError is null ? null : OperationErrorFormatter.Format(_strings, clearError);

        context.ContentHost.ShowContent(new UnsupportedRepositoryView(
            _strings,
            actualVersion,
            supportedVersion,
            failure,
            () => ShowClearUnsupportedRepositoryConfirmation(
                context,
                formatError,
                completion,
                cancellationToken),
            context.RequestStop));
    }

    private void ShowClearUnsupportedRepositoryConfirmation(
        TerminalFlowContext context,
        OperationError formatError,
        TaskCompletionSource completion,
        CancellationToken cancellationToken)
    {
        context.ContentHost.ShowContent(new ClearUnsupportedRepositoryConfirmationView(
            _strings,
            () => ClearUnsupportedRepositoryAsync(
                context,
                formatError,
                completion,
                cancellationToken),
            () => ShowUnsupportedRepository(context, formatError, completion, cancellationToken)));
    }

    private async Task ClearUnsupportedRepositoryAsync(
        TerminalFlowContext context,
        OperationError formatError,
        TaskCompletionSource completion,
        CancellationToken cancellationToken)
    {
        if (_isWorking)
        {
            return;
        }

        _isWorking = true;

        try
        {
            var result = await DispatchAsync<
                ClearUnsupportedRepositoryRequest,
                OperationResult<RepositoryClearResult>>(
                new ClearUnsupportedRepositoryRequest(),
                cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            if (result is OperationSucceeded<RepositoryClearResult>)
            {
                completion.TrySetResult();

                return;
            }

            if (result is not OperationFailed<RepositoryClearResult> failed)
            {
                throw new ArgumentOutOfRangeException(nameof(result));
            }

            ShowUnsupportedRepository(
                context,
                formatError,
                completion,
                cancellationToken,
                failed.Error);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception exception)
        {
            SetOperationFailure(completion, cancellationToken, exception);
        }
        finally
        {
            _isWorking = false;
        }
    }

    private void ShowRecoveryFailure(
        TerminalFlowContext context,
        TaskCompletionSource completion,
        CancellationToken cancellationToken)
    {
        _recovery = FailedRecovery();
        ShowRecoveryResult(context, completion, cancellationToken);
    }

    private void ShowRecoveryResult(
        TerminalFlowContext context,
        TaskCompletionSource completion,
        CancellationToken cancellationToken)
    {
        if (_recovery.Status is RepositoryRecoveryStatus.RecoveryRequired or RepositoryRecoveryStatus.Locked)
        {
            context.ContentHost.ShowContent(new RepositoryRecoveryView(
                _strings,
                _recovery,
                gameDirectory => PreviewRecoveryAsync(
                    context,
                    gameDirectory,
                    completion,
                    cancellationToken),
                () => RetryRepositoryInitializationAsync(
                    context,
                    completion,
                    cancellationToken),
                context.RequestStop));

            return;
        }

        completion.TrySetResult();
    }

    private static void SetOperationFailure(
        TaskCompletionSource completion,
        CancellationToken cancellationToken,
        Exception exception)
    {
        if (!cancellationToken.IsCancellationRequested)
        {
            completion.TrySetException(exception);
        }
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

    private async Task<TResponse> DispatchAsync<TRequest, TResponse>(
        TRequest request,
        CancellationToken cancellationToken)
        where TRequest : IRequest<TResponse>
    {
        using IServiceScope scope = _scopeFactory.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IRequestDispatcher>();

        return await dispatcher.DispatchAsync<TRequest, TResponse>(request, cancellationToken);
    }
}

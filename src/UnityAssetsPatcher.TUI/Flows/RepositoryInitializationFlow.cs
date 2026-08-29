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

    public RepositoryInitializationFlow(IServiceScopeFactory scopeFactory)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);

        _scopeFactory = scopeFactory;
        _strings = new LocalizedStrings(CultureInfo.CurrentUICulture);
    }

    public async Task RunAsync(TerminalLifecycleContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var completion = new TaskCompletionSource<object?>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        cancellationToken.ThrowIfCancellationRequested();
        var result = InitializeRepository(cancellationToken);

        bool started = context.UIDispatcher.TryInvoke(
            () => RunOnUi(
                completion,
                cancellationToken,
                () => ShowInitializationResult(context, result, completion, cancellationToken)),
            cancellationToken);

        if (!started)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new InvalidOperationException(
                "The terminal UI is no longer accepting repository initialization updates.");
        }

        await completion.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    private OperationResult<RepositoryRecoveryReport> InitializeRepository(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using IServiceScope scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRepository>();

        return new RepositoryInitializationModule(repository).Initialize();
    }

    private void ShowInitializationResult(
        TerminalLifecycleContext context,
        OperationResult<RepositoryRecoveryReport> result,
        TaskCompletionSource<object?> completion,
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

    private void PreviewRecovery(
        TerminalLifecycleContext context,
        string gameDirectory,
        TaskCompletionSource<object?> completion,
        CancellationToken cancellationToken)
    {
        bool started = context.TaskRunner.TryRun(
            () => DispatchAsync<PreviewRecoveryRequest, OperationResult<RepositoryRecoveryPreview>>(
                new PreviewRecoveryRequest(gameDirectory),
                cancellationToken),
            result =>
                RunOnUi(completion, cancellationToken, () =>
                {
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
                        () => Recover(context, preview.GameDirectory!, completion, cancellationToken),
                        () => ShowRecoveryResult(context, completion, cancellationToken),
                        context.RequestStop));
                }),
            exception => SetOperationFailure(completion, cancellationToken, exception));

        if (!started)
        {
            RunOnUi(
                completion,
                cancellationToken,
                () => ShowRecoveryFailure(context, completion, cancellationToken));
        }
    }

    private void RetryRepositoryInitialization(
        TerminalLifecycleContext context,
        TaskCompletionSource<object?> completion,
        CancellationToken cancellationToken)
    {
        bool started = context.TaskRunner.TryRun(
            () => Task.FromResult(InitializeRepository(cancellationToken)),
            result => RunOnUi(
                completion,
                cancellationToken,
                () => ShowInitializationResult(context, result, completion, cancellationToken)),
            exception => SetOperationFailure(completion, cancellationToken, exception));

        if (!started)
        {
            RunOnUi(
                completion,
                cancellationToken,
                () => ShowRecoveryFailure(context, completion, cancellationToken));
        }
    }

    private void Recover(
        TerminalLifecycleContext context,
        string gameDirectory,
        TaskCompletionSource<object?> completion,
        CancellationToken cancellationToken)
    {
        RunRecoveryOperation(
            context,
            () => DispatchAsync<RecoverRecoveryRequest, OperationResult<RepositoryRecoveryReport>>(
                new RecoverRecoveryRequest(gameDirectory),
                cancellationToken),
            completion,
            cancellationToken);
    }

    private void ShowUnsupportedRepository(
        TerminalLifecycleContext context,
        OperationError formatError,
        TaskCompletionSource<object?> completion,
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
        TerminalLifecycleContext context,
        OperationError formatError,
        TaskCompletionSource<object?> completion,
        CancellationToken cancellationToken)
    {
        context.ContentHost.ShowContent(new ClearUnsupportedRepositoryConfirmationView(
            _strings,
            () => ClearUnsupportedRepository(context, formatError, completion, cancellationToken),
            () => ShowUnsupportedRepository(context, formatError, completion, cancellationToken)));
    }

    private void ClearUnsupportedRepository(
        TerminalLifecycleContext context,
        OperationError formatError,
        TaskCompletionSource<object?> completion,
        CancellationToken cancellationToken)
    {
        bool started = context.TaskRunner.TryRun(
            () => DispatchAsync<ClearUnsupportedRepositoryRequest, OperationResult<RepositoryClearResult>>(
                new ClearUnsupportedRepositoryRequest(),
                cancellationToken),
            result =>
                RunOnUi(completion, cancellationToken, () =>
                {
                    if (result is OperationSucceeded<RepositoryClearResult>)
                    {
                        completion.TrySetResult(null);

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
                }),
            exception => SetOperationFailure(completion, cancellationToken, exception));

        if (!started)
        {
            RunOnUi(
                completion,
                cancellationToken,
                () => ShowUnsupportedRepository(
                    context,
                    formatError,
                    completion,
                    cancellationToken,
                    new OperationError(RepositoryErrorCodes.OperationAlreadyRunning)));
        }
    }

    private void RunRecoveryOperation(
        TerminalLifecycleContext context,
        Func<Task<OperationResult<RepositoryRecoveryReport>>> operation,
        TaskCompletionSource<object?> completion,
        CancellationToken cancellationToken)
    {
        bool started = context.TaskRunner.TryRun(
            operation,
            result =>
                RunOnUi(completion, cancellationToken, () =>
                {
                    _recovery = result switch
                    {
                        OperationSucceeded<RepositoryRecoveryReport> succeeded => succeeded.Value,
                        OperationFailed<RepositoryRecoveryReport> failed => failed.Error.Recovery ?? FailedRecovery(),
                        _ => throw new ArgumentOutOfRangeException(nameof(result))
                    };

                    ShowRecoveryResult(context, completion, cancellationToken);
                }),
            exception => SetOperationFailure(completion, cancellationToken, exception));

        if (!started)
        {
            RunOnUi(
                completion,
                cancellationToken,
                () => ShowRecoveryFailure(context, completion, cancellationToken));
        }
    }

    private void ShowRecoveryFailure(
        TerminalLifecycleContext context,
        TaskCompletionSource<object?> completion,
        CancellationToken cancellationToken)
    {
        _recovery = FailedRecovery();
        ShowRecoveryResult(context, completion, cancellationToken);
    }

    private void ShowRecoveryResult(
        TerminalLifecycleContext context,
        TaskCompletionSource<object?> completion,
        CancellationToken cancellationToken)
    {
        if (_recovery.Status is RepositoryRecoveryStatus.RecoveryRequired or RepositoryRecoveryStatus.Locked)
        {
            context.ContentHost.ShowContent(new RepositoryRecoveryView(
                _strings,
                _recovery,
                gameDirectory => PreviewRecovery(context, gameDirectory, completion, cancellationToken),
                () => RetryRepositoryInitialization(context, completion, cancellationToken),
                context.RequestStop));

            return;
        }

        completion.TrySetResult(null);
    }

    private static void RunOnUi(
        TaskCompletionSource<object?> completion,
        CancellationToken cancellationToken,
        Action action)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        try
        {
            action();
        }
        catch (Exception exception)
        {
            completion.TrySetException(exception);
        }
    }

    private static void SetOperationFailure(
        TaskCompletionSource<object?> completion,
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

        return await dispatcher.DispatchAsync<TRequest, TResponse>(request, cancellationToken).ConfigureAwait(false);
    }
}

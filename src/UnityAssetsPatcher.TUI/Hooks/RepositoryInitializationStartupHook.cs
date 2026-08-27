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

namespace UnityAssetsPatcher.TUI.Hooks;

public sealed class RepositoryInitializationStartupHook : ITerminalStartupHook
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly LocalizedStrings _strings;
    private RepositoryRecoveryReport _recovery = RepositoryRecoveryReport.Clean;

    public RepositoryInitializationStartupHook(IServiceScopeFactory scopeFactory)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);

        _scopeFactory = scopeFactory;
        _strings = new LocalizedStrings(CultureInfo.CurrentUICulture);
    }

    public Task RunAsync(TerminalLifecycleContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        OperationResult<RepositoryRecoveryReport> result;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            result = InitializeRepository();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Task.CompletedTask;
        }
        catch (Exception)
        {
            result = new OperationFailed<RepositoryRecoveryReport>(
                new OperationError(RepositoryErrorCodes.Unsafe));
        }

        context.UIDispatcher.TryInvoke(
            () => ShowInitializationResult(context, result),
            cancellationToken);

        return Task.CompletedTask;
    }

    private OperationResult<RepositoryRecoveryReport> InitializeRepository()
    {
        using IServiceScope scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRepository>();

        return new RepositoryInitializationModule(repository).Initialize();
    }

    private void ShowInitializationResult(
        TerminalLifecycleContext context,
        OperationResult<RepositoryRecoveryReport> result)
    {
        if (result is OperationFailed<RepositoryRecoveryReport> failed &&
            failed.Error.Code == RepositoryErrorCodes.UnsupportedVersion)
        {
            ShowUnsupportedRepository(context, failed.Error);

            return;
        }

        _recovery = result switch
        {
            OperationSucceeded<RepositoryRecoveryReport> succeeded => succeeded.Value,
            OperationFailed<RepositoryRecoveryReport> recoveryFailed =>
                recoveryFailed.Error.Recovery ?? FailedRecovery(),
            _ => throw new ArgumentOutOfRangeException(nameof(result))
        };

        ShowRecoveryResult(context);
    }

    private void PreviewRecovery(TerminalLifecycleContext context, string gameDirectory)
    {
        bool started = context.TaskRunner.TryRun(
            () => DispatchAsync<PreviewRecoveryRequest, OperationResult<RepositoryRecoveryPreview>>(
                new PreviewRecoveryRequest(gameDirectory)),
            result =>
            {
                if (result is not OperationSucceeded<RepositoryRecoveryPreview> succeeded)
                {
                    ShowRecoveryFailure(context);

                    return;
                }

                RepositoryRecoveryPreview preview = succeeded.Value;

                if (!preview.CanRecover)
                {
                    _recovery = new RepositoryRecoveryReport(preview.Status, [], preview.Issues);
                    ShowRecoveryResult(context);

                    return;
                }

                context.ContentHost.ShowContent(new RepositoryRecoveryPreviewView(
                    _strings,
                    preview,
                    () => Recover(context, preview.GameDirectory!),
                    () => ShowRecoveryResult(context),
                    context.RequestStop));
            },
            _ => ShowRecoveryFailure(context));

        if (!started)
        {
            ShowRecoveryFailure(context);
        }
    }

    private void RetryRepositoryInitialization(TerminalLifecycleContext context)
    {
        bool started = context.TaskRunner.TryRun(
            () => Task.FromResult(InitializeRepository()),
            result => ShowInitializationResult(context, result),
            _ => ShowRecoveryFailure(context));

        if (!started)
        {
            ShowRecoveryFailure(context);
        }
    }

    private void Recover(TerminalLifecycleContext context, string gameDirectory)
    {
        RunRecoveryOperation(
            context,
            () => DispatchAsync<RecoverRecoveryRequest, OperationResult<RepositoryRecoveryReport>>(
                new RecoverRecoveryRequest(gameDirectory)));
    }

    private void ShowUnsupportedRepository(
        TerminalLifecycleContext context,
        OperationError formatError,
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
            () => ShowClearUnsupportedRepositoryConfirmation(context, formatError),
            context.RequestStop));
    }

    private void ShowClearUnsupportedRepositoryConfirmation(
        TerminalLifecycleContext context,
        OperationError formatError)
    {
        context.ContentHost.ShowContent(new ClearUnsupportedRepositoryConfirmationView(
            _strings,
            () => ClearUnsupportedRepository(context, formatError),
            () => ShowUnsupportedRepository(context, formatError)));
    }

    private void ClearUnsupportedRepository(
        TerminalLifecycleContext context,
        OperationError formatError)
    {
        bool started = context.TaskRunner.TryRun(
            () => DispatchAsync<ClearUnsupportedRepositoryRequest, OperationResult<RepositoryClearResult>>(
                new ClearUnsupportedRepositoryRequest()),
            result =>
            {
                if (result is OperationSucceeded<RepositoryClearResult>)
                {
                    context.Navigator.ShowMainMenu();

                    return;
                }

                var failed = (OperationFailed<RepositoryClearResult>)result;
                ShowUnsupportedRepository(context, formatError, failed.Error);
            },
            _ => ShowUnsupportedRepository(
                context,
                formatError,
                new OperationError(RepositoryErrorCodes.Unsafe)));

        if (!started)
        {
            ShowUnsupportedRepository(
                context,
                formatError,
                new OperationError(RepositoryErrorCodes.OperationAlreadyRunning));
        }
    }

    private void RunRecoveryOperation(
        TerminalLifecycleContext context,
        Func<Task<OperationResult<RepositoryRecoveryReport>>> operation)
    {
        bool started = context.TaskRunner.TryRun(
            operation,
            result =>
            {
                _recovery = result switch
                {
                    OperationSucceeded<RepositoryRecoveryReport> succeeded => succeeded.Value,
                    OperationFailed<RepositoryRecoveryReport> failed => failed.Error.Recovery ?? FailedRecovery(),
                    _ => throw new ArgumentOutOfRangeException(nameof(result))
                };

                ShowRecoveryResult(context);
            },
            _ => ShowRecoveryFailure(context));

        if (!started)
        {
            ShowRecoveryFailure(context);
        }
    }

    private void ShowRecoveryFailure(TerminalLifecycleContext context)
    {
        _recovery = FailedRecovery();
        ShowRecoveryResult(context);
    }

    private void ShowRecoveryResult(TerminalLifecycleContext context)
    {
        if (_recovery.Status is RepositoryRecoveryStatus.RecoveryRequired or RepositoryRecoveryStatus.Locked)
        {
            context.ContentHost.ShowContent(new RepositoryRecoveryView(
                _strings,
                _recovery,
                gameDirectory => PreviewRecovery(context, gameDirectory),
                () => RetryRepositoryInitialization(context),
                context.RequestStop));

            return;
        }

        context.Navigator.ShowMainMenu();
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

    private async Task<TResponse> DispatchAsync<TRequest, TResponse>(TRequest request)
        where TRequest : IRequest<TResponse>
    {
        using IServiceScope scope = _scopeFactory.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IRequestDispatcher>();

        return await dispatcher.DispatchAsync<TRequest, TResponse>(request).ConfigureAwait(false);
    }
}

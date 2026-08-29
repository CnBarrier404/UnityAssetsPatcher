using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using UnityAssetsPatcher.Application;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.TUI.Flows;
using UnityAssetsPatcher.TUI.Framework;
using UnityAssetsPatcher.TUI.Localization;
using UnityAssetsPatcher.TUI.Navigation;
using UnityAssetsPatcher.TUI.Shell;

namespace UnityAssetsPatcher.TUI.Lifecycle;

internal sealed class TerminalSession
{
    private readonly RepositoryInitializationFlow _repositoryInitialization;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AppRuntimeConfig _runtimeConfig;
    private readonly ILoggingLevelSwitch? _loggingLevelSwitch;
    private readonly ILogger<TerminalSession> _logger;

    public TerminalSession(
        IServiceScopeFactory scopeFactory,
        AppRuntimeConfig runtimeConfig,
        ILoggingLevelSwitch? loggingLevelSwitch = null,
        ILogger<TerminalSession>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(runtimeConfig);

        _repositoryInitialization = new RepositoryInitializationFlow(scopeFactory);
        _scopeFactory = scopeFactory;
        _runtimeConfig = runtimeConfig;
        _loggingLevelSwitch = loggingLevelSwitch;
        _logger = logger ?? NullLogger<TerminalSession>.Instance;
    }

    public async Task RunAsync()
    {
        using IApplication application = Terminal.Gui.App.Application.Create();

        application.Init(OperatingSystem.IsWindows() ? DriverRegistry.Names.WINDOWS : null);

        bool isLegacyConsole = application.Driver?.IsLegacyConsole == true;

        TerminalTheme.Initialize(isLegacyConsole);

        CultureInfo culture = CultureInfo.CurrentUICulture;
        var strings = new LocalizedStrings(culture);
        string? warningText = isLegacyConsole ? strings.Layout_LegacyConsoleWarning : null;

        using TerminalShellView shell = new(
            strings.Layout_ShortcutHint,
            warningText,
            () => application.LayoutAndDraw());
        using var sessionCancellation = new CancellationTokenSource();
        Task? startupTask = null;
        bool isStopPending = false;
        bool canStop = false;

        var navigator = new TerminalNavigator(
            shell,
            culture,
            _scopeFactory,
            _runtimeConfig,
            _loggingLevelSwitch);

        var context = new TerminalFlowContext(
            shell,
            () => application.RequestStop(shell));

        void StartSession(object? sender, EventArgs eventArgs)
        {
            shell.Initialized -= StartSession;
            startupTask = RunStartupAsync(context, navigator, sessionCancellation.Token);
        }

        void CancelStartupBeforeStopping(object? sender, CancelEventArgs<bool> eventArgs)
        {
            if (eventArgs.NewValue || canStop)
            {
                return;
            }

            sessionCancellation.Cancel();

            if (startupTask is null || startupTask.IsCompleted)
            {
                return;
            }

            eventArgs.Cancel = true;

            if (isStopPending)
            {
                return;
            }

            isStopPending = true;
            _ = RequestStopAfterStartupAsync(startupTask);
        }

        async Task RequestStopAfterStartupAsync(Task task)
        {
            try
            {
                await task;
            }
            catch
            {
                // RunStartupAsync logs failures; RunAsync observes them after Run returns.
            }

            application.AddTimeout(
                TimeSpan.Zero,
                () =>
                {
                    canStop = true;
                    application.RequestStop(shell);
                    return false;
                });
        }

        shell.Initialized += StartSession;
        shell.IsRunningChanging += CancelStartupBeforeStopping;

        _logger.LogInformation("Terminal application started");

        try
        {
            application.Run(shell);
        }
        finally
        {
            shell.IsRunningChanging -= CancelStartupBeforeStopping;
            await sessionCancellation.CancelAsync().ConfigureAwait(false);

            if (startupTask is not null)
            {
                try
                {
                    await startupTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (sessionCancellation.IsCancellationRequested) { }
            }
        }
    }

    private async Task RunStartupAsync(
        TerminalFlowContext context,
        TerminalNavigator navigator,
        CancellationToken cancellationToken)
    {
        try
        {
            await _repositoryInitialization.RunAsync(context, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();
            navigator.ShowMainMenu();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Terminal startup failed.");

            try
            {
                context.RequestStop();
            }
            catch (Exception stopException)
            {
                _logger.LogError(stopException, "Terminal session could not request stop.");
            }

            throw;
        }
    }
}

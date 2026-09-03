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

        var renderer = new ApplicationRenderer(application);
        using TerminalShellView shell = new(
            strings.Layout_ShortcutHint,
            warningText,
            renderer.Render);

        var navigator = new TerminalNavigator(
            shell,
            TerminalRouteTable.Create(
                culture,
                _scopeFactory,
                _runtimeConfig,
                _loggingLevelSwitch));

        var runState = new RunState(this, application, shell, navigator);
        var context = new TerminalFlowContext(
            shell,
            runState.RequestStop);

        await runState.RunAsync(context);
    }

    private async Task RunStartupAsync(
        TerminalFlowContext context,
        TerminalNavigator navigator,
        IApplication application,
        CancellationToken cancellationToken)
    {
        try
        {
            await _repositoryInitialization.RunAsync(context, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();
            application.Invoke(() =>
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    navigator.Navigate(TerminalRoute.MainMenu);
                }
            });
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

    private sealed class ApplicationRenderer(IApplication application)
    {
        public void Render()
        {
            application.LayoutAndDraw();
        }
    }

    private sealed class RunState(
        TerminalSession session,
        IApplication application,
        TerminalShellView shell,
        TerminalNavigator navigator)
    {
        private CancellationTokenSource? _sessionCancellation;
        private TerminalFlowContext? _context;
        private Task? _startupTask;
        private bool _isStopPending;
        private bool _canStop;

        public async Task RunAsync(TerminalFlowContext context)
        {
            using var sessionCancellation = new CancellationTokenSource();
            _sessionCancellation = sessionCancellation;
            _context = context;

            shell.Initialized += StartSession;
            shell.IsRunningChanging += CancelStartupBeforeStopping;

            session._logger.LogInformation("Terminal application started");

            try
            {
                await application.RunAsync(shell, sessionCancellation.Token);
            }
            finally
            {
                shell.Initialized -= StartSession;
                shell.IsRunningChanging -= CancelStartupBeforeStopping;
                await sessionCancellation.CancelAsync().ConfigureAwait(false);

                if (_startupTask is not null)
                {
                    try
                    {
                        await _startupTask.ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (sessionCancellation.IsCancellationRequested) { }
                }
            }
        }

        public void RequestStop()
        {
            application.RequestStop(shell);
        }

        private void StartSession(object? sender, EventArgs eventArgs)
        {
            shell.Initialized -= StartSession;
            _startupTask = session.RunStartupAsync(
                _context!,
                navigator,
                application,
                _sessionCancellation!.Token);
        }

        private void CancelStartupBeforeStopping(
            object? sender,
            CancelEventArgs<bool> eventArgs)
        {
            if (eventArgs.NewValue || _canStop)
            {
                return;
            }

            _sessionCancellation!.Cancel();

            if (_startupTask is null || _startupTask.IsCompleted)
            {
                return;
            }

            eventArgs.Cancel = true;

            if (_isStopPending)
            {
                return;
            }

            _isStopPending = true;
            _ = RequestStopAfterStartupAsync(_startupTask);
        }

        private async Task RequestStopAfterStartupAsync(Task task)
        {
            try
            {
                await task;
            }
            catch
            {
                // RunStartupAsync logs failures; RunAsync observes them after Run returns.
            }

            application.AddTimeout(TimeSpan.Zero, StopAfterStartup);
        }

        private bool StopAfterStartup()
        {
            _canStop = true;
            RequestStop();
            return false;
        }
    }
}

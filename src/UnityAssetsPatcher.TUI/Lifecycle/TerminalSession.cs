using System.Globalization;
using System.Runtime.ExceptionServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using UnityAssetsPatcher.TUI.Flows;
using UnityAssetsPatcher.TUI.Framework;
using UnityAssetsPatcher.TUI.Localization;
using UnityAssetsPatcher.TUI.Navigation;
using UnityAssetsPatcher.TUI.Shell;

namespace UnityAssetsPatcher.TUI.Lifecycle;

internal sealed class TerminalSession
{
    private readonly RepositoryInitializationFlow _repositoryInitialization;
    private readonly TerminalRouteTable _routeTable;
    private readonly ILogger<TerminalSession> _logger;

    public TerminalSession(
        IServiceScopeFactory scopeFactory,
        TerminalRouteTable routeTable,
        ILogger<TerminalSession>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(routeTable);

        _repositoryInitialization = new RepositoryInitializationFlow(scopeFactory);
        _routeTable = routeTable;
        _logger = logger ?? NullLogger<TerminalSession>.Instance;
    }

    public async Task RunAsync()
    {
        IApplication application = Terminal.Gui.App.Application.Create();
        TerminalShellView? shell = null;
        Exception? runFailure = null;
        try
        {
            application.Init(OperatingSystem.IsWindows() ? DriverRegistry.Names.WINDOWS : null);

            bool isLegacyConsole = application.Driver?.IsLegacyConsole == true;

            TerminalTheme.Initialize(isLegacyConsole);

            CultureInfo culture = CultureInfo.CurrentUICulture;
            var strings = new LocalizedStrings(culture);
            string? warningText = isLegacyConsole ? strings.Layout_LegacyConsoleWarning : null;

            var renderer = new ApplicationRenderer(application);
            shell = new TerminalShellView(
                strings.Layout_ShortcutHint,
                warningText,
                renderer.Render);

            var navigator = new TerminalNavigator(
                shell,
                _routeTable.Create(culture));

            var runState = new RunState(this, application, shell, navigator);
            var context = new TerminalFlowContext(
                shell,
                runState.RequestStop,
                runState.InvokeAsync);

            await runState.RunAsync(context);
        }
        catch (Exception exception)
        {
            runFailure = exception;
            throw;
        }
        finally
        {
            List<Exception> cleanupFailures = [];
            IDisposable[] resources = shell is null ? [application] : [shell, application];
            foreach (IDisposable resource in resources)
            {
                try
                {
                    resource.Dispose();
                }
                catch (Exception exception)
                {
                    cleanupFailures.Add(exception);
                }
            }

            ThrowCleanupFailures(runFailure, cleanupFailures);
        }
    }

    private async Task RunStartupAsync(
        TerminalFlowContext context,
        TerminalNavigator navigator,
        CancellationToken cancellationToken)
    {
        await _repositoryInitialization.RunAsync(context, cancellationToken).ConfigureAwait(false);

        await context.InvokeAsync(
            () => navigator.Navigate(TerminalRoute.MainMenu),
            cancellationToken).ConfigureAwait(false);
    }

    private static void ThrowCleanupFailures(Exception? runFailure, List<Exception> cleanupFailures)
    {
        if (cleanupFailures.Count == 0)
        {
            return;
        }

        if (runFailure is not null)
        {
            throw new AggregateException([runFailure, .. cleanupFailures]);
        }

        if (cleanupFailures.Count == 1)
        {
            ExceptionDispatchInfo.Capture(cleanupFailures[0]).Throw();
        }

        throw new AggregateException(cleanupFailures);
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

            Exception? runFailure = null;
            try
            {
                // RunState owns cancellation and stopping; preserve exceptions from the loop unchanged.
                await application.RunAsync(shell, CancellationToken.None);
            }
            catch (Exception exception)
            {
                runFailure = exception;
                throw;
            }
            finally
            {
                shell.Initialized -= StartSession;
                shell.IsRunningChanging -= CancelStartupBeforeStopping;
                List<Exception> cleanupFailures = [];
                try
                {
                    sessionCancellation.Cancel();
                }
                catch (Exception exception)
                {
                    cleanupFailures.Add(exception);
                }

                if (_startupTask is not null)
                {
                    try
                    {
                        // The loop has ended. Startup continuations do not require the UI thread,
                        // and queued UI work is canceled, so drain before disposing UI resources here.
                        _startupTask.GetAwaiter().GetResult();
                    }
                    catch (Exception exception)
                    {
                        if (!ReferenceEquals(exception, runFailure))
                        {
                            cleanupFailures.Add(exception);
                        }
                    }
                }

                ThrowCleanupFailures(runFailure, cleanupFailures);
            }
        }

        public void RequestStop()
        {
            application.RequestStop(shell);
        }

        public async Task InvokeAsync(Action action, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Lock gate = new();
            bool started = false;
            using CancellationTokenRegistration registration = cancellationToken.Register(() =>
            {
                lock (gate)
                {
                    if (!started)
                    {
                        completion.TrySetCanceled(cancellationToken);
                    }
                }
            });

            application.Invoke(() =>
            {
                lock (gate)
                {
                    if (completion.Task.IsCompleted)
                    {
                        return;
                    }

                    started = true;
                    try
                    {
                        action();
                        completion.TrySetResult();
                    }
                    catch (Exception exception)
                    {
                        completion.TrySetException(exception);
                    }
                }
            });

            await completion.Task.ConfigureAwait(false);
        }

        private async void StartSession(object? sender, EventArgs eventArgs)
        {
            shell.Initialized -= StartSession;
            await (_startupTask = RunStartupAsync(_sessionCancellation!.Token));
        }

        private async Task RunStartupAsync(CancellationToken cancellationToken)
        {
            try
            {
                await session.RunStartupAsync(_context!, navigator, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested &&
                                                               exception.CancellationToken == cancellationToken) { }
        }

        private async void CancelStartupBeforeStopping(
            object? sender,
            CancelEventArgs<bool> eventArgs)
        {
            await StopAfterStartupAsync(eventArgs);
        }

        private async Task StopAfterStartupAsync(CancelEventArgs<bool> eventArgs)
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
            // StartSession propagates startup failures; this handler waits only for completion
            // so a normal stop keeps the loop alive while startup acknowledges cancellation.
            await Task.WhenAny(_startupTask);
            if (_startupTask.IsCompletedSuccessfully)
            {
                application.AddTimeout(TimeSpan.Zero, StopAfterStartup);
            }
        }

        private bool StopAfterStartup()
        {
            _canStop = true;
            RequestStop();
            return false;
        }
    }
}

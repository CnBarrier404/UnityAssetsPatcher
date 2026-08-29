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
        var uiDispatcher = new TerminalUIDispatcher(application);
        var taskRunner = new TerminalTaskRunner(callback => uiDispatcher.TryInvoke(callback));
        var navigator = new TerminalNavigator(
            shell,
            culture,
            _scopeFactory,
            _runtimeConfig,
            _loggingLevelSwitch,
            uiDispatcher,
            taskRunner,
            () => WindowsNativeFilePicker.PickFile(
                strings.InstallPage_SelectModDialogTitle,
                strings.InstallPage_ModZipFileType));

        var context = new TerminalLifecycleContext(uiDispatcher, navigator, shell, taskRunner, application.RequestStop);
        using var sessionCancellation = new CancellationTokenSource();
        Task startupTask = Task.Run(
            () => RunStartupAsync(context, sessionCancellation.Token),
            CancellationToken.None);

        _logger.LogInformation("Terminal application started");

        try
        {
            await application.RunAsync(shell, sessionCancellation.Token);
        }
        finally
        {
            uiDispatcher.StopAccepting();
            await sessionCancellation.CancelAsync().ConfigureAwait(false);

            try
            {
                await startupTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (sessionCancellation.IsCancellationRequested) { }
        }
    }

    private async Task RunStartupAsync(
        TerminalLifecycleContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            await _repositoryInitialization.RunAsync(context, cancellationToken).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();

            await InvokeOnUIAsync(
                context,
                context.Navigator.ShowMainMenu,
                cancellationToken).ConfigureAwait(false);
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

    private static async Task InvokeOnUIAsync(
        TerminalLifecycleContext context,
        Action action,
        CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource<object?>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        bool started = context.UIDispatcher.TryInvoke(
            () =>
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                try
                {
                    action();
                    completion.TrySetResult(null);
                }
                catch (Exception exception)
                {
                    completion.TrySetException(exception);
                }
            },
            cancellationToken);

        if (!started)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new InvalidOperationException("The terminal UI is no longer accepting startup updates.");
        }

        await completion.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
    }
}

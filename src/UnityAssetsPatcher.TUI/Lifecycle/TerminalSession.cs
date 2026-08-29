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
        await using var taskRunner = new TerminalTaskRunner(uiDispatcher);
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

        var context = new TerminalFlowContext(
            uiDispatcher,
            shell,
            taskRunner,
            () => application.RequestStop(shell));

        void StartSession(object? sender, EventArgs eventArgs)
        {
            shell.Initialized -= StartSession;

            if (!taskRunner.TryRunBackground(cancellationToken => RunStartupAsync(
                    context,
                    navigator,
                    cancellationToken)))
            {
                application.RequestStop(shell);
            }
        }

        shell.Initialized += StartSession;

        _logger.LogInformation("Terminal application started");

        try
        {
            application.Run(shell);
        }
        finally
        {
            uiDispatcher.StopAccepting();
            await taskRunner.StopAsync().ConfigureAwait(false);
        }
    }

    private async Task RunStartupAsync(
        TerminalFlowContext context,
        TerminalNavigator navigator,
        CancellationToken cancellationToken)
    {
        try
        {
            await _repositoryInitialization.RunAsync(context, cancellationToken).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();

            await context.UIDispatcher
                .InvokeAsync(navigator.ShowMainMenu, cancellationToken)
                .ConfigureAwait(false);
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
                await context.UIDispatcher
                    .InvokeAsync(context.RequestStop, CancellationToken.None)
                    .ConfigureAwait(false);
            }
            catch (Exception stopException)
            {
                _logger.LogError(stopException, "Terminal session could not request stop.");
            }

            throw;
        }
    }
}

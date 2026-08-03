using System.Globalization;
using Microsoft.Extensions.Logging;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using UnityAssetsPatcher.Application;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Updates;
using UnityAssetsPatcher.TUI.Framework;
using UnityAssetsPatcher.TUI.Localization;
using UnityAssetsPatcher.TUI.Navigation;
using UnityAssetsPatcher.TUI.Shell;

namespace UnityAssetsPatcher.TUI;

public sealed class TerminalApp
{
    private readonly AppInfo _appInfo;
    private readonly IUpdateChecker _updateChecker;
    private readonly IWorkflowService? _workflowService;
    private readonly TerminalSettings _settings;
    private readonly ILoggingLevelSwitch? _loggingLevelSwitch;
    private readonly ILogger<TerminalApp> _logger;

    public TerminalApp(AppInfo appInfo, IUpdateChecker updateChecker, ILogger<TerminalApp> logger)
    {
        ArgumentNullException.ThrowIfNull(appInfo);
        ArgumentNullException.ThrowIfNull(updateChecker);
        ArgumentNullException.ThrowIfNull(logger);

        _appInfo = appInfo;
        _updateChecker = updateChecker;
        _settings = new TerminalSettings();
        _logger = logger;
    }

    public TerminalApp(
        AppInfo appInfo,
        IUpdateChecker updateChecker,
        IWorkflowService workflowService,
        TerminalSettings settings,
        ILoggingLevelSwitch loggingLevelSwitch,
        ILogger<TerminalApp> logger)
    {
        ArgumentNullException.ThrowIfNull(appInfo);
        ArgumentNullException.ThrowIfNull(updateChecker);
        ArgumentNullException.ThrowIfNull(workflowService);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(loggingLevelSwitch);
        ArgumentNullException.ThrowIfNull(logger);

        _appInfo = appInfo;
        _updateChecker = updateChecker;
        _workflowService = workflowService;
        _settings = settings;
        _loggingLevelSwitch = loggingLevelSwitch;
        _logger = logger;
    }

    public int Run()
    {
        try
        {
            return RunCore();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Terminal application terminated unexpectedly");

            var strings = new LocalizedStrings(CultureInfo.CurrentUICulture);
            string logDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "UnityAssetsPatcher",
                "logs");

            Console.Error.WriteLine(strings.Error_UnexpectedFormat(logDirectory));

            return 1;
        }
    }

    private int RunCore()
    {
        using IApplication application = Terminal.Gui.App.Application.Create();

        application.Init(OperatingSystem.IsWindows() ? DriverRegistry.Names.WINDOWS : null);

        bool isLegacyConsole = application.Driver?.IsLegacyConsole == true;

        TerminalTheme.Initialize(isLegacyConsole);

        CultureInfo culture = CultureInfo.CurrentUICulture;
        var strings = new LocalizedStrings(culture);
        string? warningText = isLegacyConsole ? strings.Layout_LegacyConsoleWarning : null;

        using TerminalShellView shell = new(
            _appInfo,
            strings.Layout_ShortcutHint,
            warningText,
            () => application.LayoutAndDraw());
        var taskRunner = new TerminalTaskRunner(application.Invoke);
        TerminalNavigator navigator = _workflowService is null
            ? new TerminalNavigator(shell, culture)
            : new TerminalNavigator(
                shell,
                culture,
                _workflowService,
                _settings,
                _loggingLevelSwitch,
                taskRunner,
                application.RequestStop);
        using CancellationTokenSource updateCancellation = new();

        navigator.Start();

        _ = CheckForUpdateAsync(application, navigator, updateCancellation.Token);

        _logger.LogInformation("Terminal application started");

        application.Run(shell);

        updateCancellation.Cancel();

        return 0;
    }

    private async Task CheckForUpdateAsync(
        IApplication application,
        TerminalNavigator navigator,
        CancellationToken cancellationToken)
    {
        try
        {
            UpdateCheckResult result = await _updateChecker
                .CheckForUpdateAsync(cancellationToken)
                .ConfigureAwait(false);

            if (result is not UpdateAvailable update || cancellationToken.IsCancellationRequested)
            {
                return;
            }

            try
            {
                application.Invoke(() =>
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    navigator.ShowAvailableUpdate(update.Update);

                    application.LayoutAndDraw();
                });
            }
            catch (ObjectDisposedException) { }
            catch (InvalidOperationException) { }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Update check terminated unexpectedly");
        }
    }
}

using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using UnityAssetsPatcher.Application;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Operations;
using UnityAssetsPatcher.Application.Updates;
using UnityAssetsPatcher.TUI.Framework;
using UnityAssetsPatcher.TUI.Localization;
using UnityAssetsPatcher.TUI.Navigation;
using UnityAssetsPatcher.TUI.Shell;

namespace UnityAssetsPatcher.TUI;

public sealed class TerminalApp
{
    private readonly AppInfo _appInfo;
    private readonly UpdateCheckModule _updateCheckModule;
    private readonly IServiceScopeFactory? _scopeFactory;
    private readonly TerminalSettings _settings;
    private readonly ILoggingLevelSwitch? _loggingLevelSwitch;
    private readonly ILogger<TerminalApp> _logger;

    public TerminalApp(
        AppInfo appInfo,
        UpdateCheckModule updateCheckModule,
        ILogger<TerminalApp> logger)
    {
        ArgumentNullException.ThrowIfNull(appInfo);
        ArgumentNullException.ThrowIfNull(updateCheckModule);
        ArgumentNullException.ThrowIfNull(logger);

        _appInfo = appInfo;
        _updateCheckModule = updateCheckModule;
        _settings = new TerminalSettings();
        _logger = logger;
    }

    public TerminalApp(
        AppInfo appInfo,
        UpdateCheckModule updateCheckModule,
        IServiceScopeFactory scopeFactory,
        TerminalSettings settings,
        ILoggingLevelSwitch loggingLevelSwitch,
        ILogger<TerminalApp> logger)
    {
        ArgumentNullException.ThrowIfNull(appInfo);
        ArgumentNullException.ThrowIfNull(updateCheckModule);
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(loggingLevelSwitch);
        ArgumentNullException.ThrowIfNull(logger);

        _appInfo = appInfo;
        _updateCheckModule = updateCheckModule;
        _scopeFactory = scopeFactory;
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
        TerminalNavigator navigator = _scopeFactory is null
            ? new TerminalNavigator(shell, culture)
            : new TerminalNavigator(
                shell,
                culture,
                _scopeFactory!,
                _settings,
                _loggingLevelSwitch,
                taskRunner,
                application.RequestStop,
                () => WindowsNativeFilePicker.PickFile(
                    strings.InstallPage_SelectModDialogTitle,
                    strings.InstallPage_ModZipFileType));
        using CancellationTokenSource updateCancellation = new();

        navigator.Start();

        Task updateTask = CheckForUpdateAsync(application, navigator, updateCancellation.Token);

        _logger.LogInformation("Terminal application started");

        try
        {
            application.Run(shell);
        }
        finally
        {
            updateCancellation.Cancel();

            try
            {
                updateTask.GetAwaiter().GetResult();
            }
            catch (OperationCanceledException) when (updateCancellation.IsCancellationRequested) { }
        }

        return 0;
    }

    private async Task CheckForUpdateAsync(
        IApplication application,
        TerminalNavigator navigator,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _updateCheckModule
                .CheckForUpdateAsync(cancellationToken)
                .ConfigureAwait(false);

            if (result is not OperationSucceeded<UpdateInfo?> { Value: { } update } ||
                cancellationToken.IsCancellationRequested)
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

                    navigator.ShowAvailableUpdate(update);

                    application.LayoutAndDraw();
                });
            }
            catch (ObjectDisposedException) { }
            catch (InvalidOperationException) { }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    }
}

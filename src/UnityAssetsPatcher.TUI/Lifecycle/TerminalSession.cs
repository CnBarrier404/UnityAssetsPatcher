using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using UnityAssetsPatcher.Application;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.TUI.Framework;
using UnityAssetsPatcher.TUI.Localization;
using UnityAssetsPatcher.TUI.Navigation;
using UnityAssetsPatcher.TUI.Shell;

namespace UnityAssetsPatcher.TUI.Lifecycle;

internal sealed class TerminalSession
{
    private readonly AppInfo _appInfo;
    private readonly TerminalLifecycle _lifecycle;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TerminalSettings _settings;
    private readonly ILoggingLevelSwitch? _loggingLevelSwitch;
    private readonly ILogger<TerminalSession> _logger;

    public TerminalSession(
        AppInfo appInfo,
        TerminalLifecycle lifecycle,
        IServiceScopeFactory scopeFactory,
        TerminalSettings settings,
        ILoggingLevelSwitch? loggingLevelSwitch = null,
        ILogger<TerminalSession>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(appInfo);
        ArgumentNullException.ThrowIfNull(lifecycle);
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(settings);

        _appInfo = appInfo;
        _lifecycle = lifecycle;
        _scopeFactory = scopeFactory;
        _settings = settings;
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
            _appInfo,
            strings.Layout_ShortcutHint,
            warningText,
            () => application.LayoutAndDraw());
        var uiDispatcher = new TerminalUIDispatcher(application);
        var taskRunner = new TerminalTaskRunner(callback => uiDispatcher.TryInvoke(callback));
        var navigator = new TerminalNavigator(
            shell,
            culture,
            _scopeFactory,
            _settings,
            _loggingLevelSwitch,
            taskRunner,
            () => WindowsNativeFilePicker.PickFile(
                strings.InstallPage_SelectModDialogTitle,
                strings.InstallPage_ModZipFileType));

        var context = new TerminalLifecycleContext(uiDispatcher, navigator, shell, taskRunner, application.RequestStop);

        _ = _lifecycle.Start(context);

        _logger.LogInformation("Terminal application started");

        try
        {
            application.Run(shell);
        }
        finally
        {
            uiDispatcher.StopAccepting();
            await _lifecycle.StopAsync().ConfigureAwait(false);
        }
    }
}

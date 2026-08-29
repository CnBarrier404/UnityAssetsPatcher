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
    private readonly TerminalLifecycle _lifecycle;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AppRuntimeConfig _runtimeConfig;
    private readonly ILoggingLevelSwitch? _loggingLevelSwitch;
    private readonly ILogger<TerminalSession> _logger;

    public TerminalSession(
        TerminalLifecycle lifecycle,
        IServiceScopeFactory scopeFactory,
        AppRuntimeConfig runtimeConfig,
        ILoggingLevelSwitch? loggingLevelSwitch = null,
        ILogger<TerminalSession>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(lifecycle);
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(runtimeConfig);

        _lifecycle = lifecycle;
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
        using var lifecycleCancellation = new CancellationTokenSource();
        Task lifecycleTask = Task.Run(
            () => _lifecycle.RunAsync(context, lifecycleCancellation.Token),
            CancellationToken.None);

        _logger.LogInformation("Terminal application started");

        try
        {
            await application.RunAsync(shell, lifecycleCancellation.Token);
        }
        finally
        {
            uiDispatcher.StopAccepting();
            await lifecycleCancellation.CancelAsync().ConfigureAwait(false);

            try
            {
                await lifecycleTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (lifecycleCancellation.IsCancellationRequested) { }
        }
    }
}

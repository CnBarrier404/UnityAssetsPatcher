using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using UnityAssetsPatcher.Application;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.TUI.Framework;
using UnityAssetsPatcher.TUI.Localization;
using UnityAssetsPatcher.TUI.Pages;
using UnityAssetsPatcher.TUI.Shell;

namespace UnityAssetsPatcher.TUI.Navigation;

public sealed class TerminalGUINavigator
{
    private readonly AppInfo _appInfo;
    private readonly IUpdateChecker _updateChecker;
    private readonly IWorkflowService _workflowService;
    private readonly TerminalSettings _settings;
    private readonly ILoggingLevelSwitch? _loggingLevelSwitch;

    public TerminalGUINavigator(
        AppInfo appInfo,
        IUpdateChecker updateChecker,
        IWorkflowService workflowService,
        TerminalSettings settings,
        ILoggingLevelSwitch? loggingLevelSwitch = null)
    {
        _appInfo = appInfo;
        _updateChecker = updateChecker;
        _workflowService = workflowService;
        _settings = settings;
        _loggingLevelSwitch = loggingLevelSwitch;
    }

    public int Run()
    {
        using IApplication application = Terminal.Gui.App.Application.Create();
        application.Init(OperatingSystem.IsWindows() ? DriverRegistry.Names.WINDOWS : null);
        bool isLegacyConsole = application.Driver?.IsLegacyConsole == true;
        TerminalTheme.Initialize(isLegacyConsole);
        var taskRunner = new TerminalTaskRunner(application.Invoke);
        var menuItems = CreateMenuItems(taskRunner);
        string? warningText = isLegacyConsole
            ? LocalizedStrings.Layout_LegacyConsoleWarning
            : null;
        using var shell = new TerminalShellView(
            application,
            _appInfo,
            LocalizedStrings.Layout_ShortcutHint,
            warningText);
        using var updateCancellation = new CancellationTokenSource();
        AvailableUpdate? availableUpdate = null;
        MainMenuView? visibleMainMenu = null;
        BackupRecoveryReport recovery = BackupRecoveryReport.Clean;
        int recoveryRunning = 0;

        _ = CheckBackupAsync();
        _ = CheckForUpdateAsync();
        application.Run(shell);
        updateCancellation.Cancel();

        return 0;

        async Task CheckBackupAsync()
        {
            await RunBackupTaskAsync(_workflowService.CheckPendingTransactions).ConfigureAwait(false);
        }

        async Task PreviewBackupAsync(string gameDirectory)
        {
            if (Interlocked.Exchange(ref recoveryRunning, 1) == 1) return;
            BackupRecoveryPreview preview;
            try
            {
                OperationResult<BackupRecoveryPreview> result =
                    await Task.Run(() => _workflowService.PreviewPendingTransaction(gameDirectory))
                        .ConfigureAwait(false);
                preview = result switch
                {
                    OperationSucceeded<BackupRecoveryPreview> succeeded => succeeded.Value,
                    OperationFailed<BackupRecoveryPreview> => FailedPreview(),
                    _ => throw new ArgumentOutOfRangeException(nameof(result)),
                };
            }
            catch (Exception)
            {
                preview = new BackupRecoveryPreview(BackupRepositoryStatus.Locked, null, null, null, null, false, [],
                    [new BackupRecoveryIssue(BackupRecoveryIssueCode.UnexpectedFailure, string.Empty)]);
            }
            finally
            {
                Volatile.Write(ref recoveryRunning, 0);
            }

            application.Invoke(() =>
            {
                if (!preview.CanRecover)
                {
                    recovery = new BackupRecoveryReport(preview.Status, [], preview.Issues);
                    ShowRecoveryResult();
                    return;
                }

                shell.ShowContent(new BackupRecoveryPreviewView(
                    preview,
                    () => _ = RecoverBackupAsync(preview.GameDirectory!),
                    ShowRecoveryResult,
                    application.RequestStop));
            });
        }

        async Task RecoverBackupAsync(string gameDirectory)
        {
            await RunBackupTaskAsync(() => _workflowService.RecoverPendingTransactions(gameDirectory))
                .ConfigureAwait(false);
        }

        async Task RunBackupTaskAsync(Func<OperationResult<BackupRecoveryReport>> operation)
        {
            if (Interlocked.Exchange(ref recoveryRunning, 1) == 1)
            {
                return;
            }

            try
            {
                OperationResult<BackupRecoveryReport> result = await Task.Run(operation).ConfigureAwait(false);
                recovery = result switch
                {
                    OperationSucceeded<BackupRecoveryReport> succeeded => succeeded.Value,
                    OperationFailed<BackupRecoveryReport> failed => failed.Error.Recovery ??
                                                                    new BackupRecoveryReport(
                                                                        BackupRepositoryStatus.Locked,
                                                                        [],
                                                                        [
                                                                            new BackupRecoveryIssue(
                                                                                BackupRecoveryIssueCode.OperationFailed,
                                                                                string.Empty)
                                                                        ]),
                    _ => throw new ArgumentOutOfRangeException(nameof(result)),
                };
            }
            catch (Exception)
            {
                recovery = new BackupRecoveryReport(BackupRepositoryStatus.Locked, [],
                    [new BackupRecoveryIssue(BackupRecoveryIssueCode.UnexpectedFailure, string.Empty)]);
            }
            finally
            {
                Volatile.Write(ref recoveryRunning, 0);
            }

            ShowRecoveryResult();
        }

        BackupRecoveryPreview FailedPreview()
        {
            return new BackupRecoveryPreview(
                BackupRepositoryStatus.Locked,
                null,
                null,
                null,
                null,
                false,
                [],
                [new BackupRecoveryIssue(BackupRecoveryIssueCode.OperationFailed, string.Empty)]);
        }

        void ShowRecoveryResult()
        {
            try
            {
                application.Invoke(() =>
                {
                    if (recovery.Status is BackupRepositoryStatus.RecoveryRequired or BackupRepositoryStatus.Locked)
                    {
                        shell.ShowContent(new BackupRecoveryView(
                            recovery,
                            gameDirectory => _ = PreviewBackupAsync(gameDirectory),
                            () => _ = CheckBackupAsync(),
                            application.RequestStop));
                        return;
                    }

                    ShowMainMenu();
                });
            }
            catch (ObjectDisposedException) { }
            catch (InvalidOperationException) { }
        }

        void ShowMainMenu()
        {
            var mainMenu = new MainMenuView(menuItems, availableUpdate, recovery);
            visibleMainMenu = mainMenu;
            mainMenu.ItemSelected += (_, item) =>
            {
                visibleMainMenu = null;
                shell.ShowContent(item.CreateView(ShowMainMenu));
            };

            shell.ShowContent(mainMenu);
        }

        async Task CheckForUpdateAsync()
        {
            UpdateCheckResult result = await _updateChecker.CheckForUpdateAsync(updateCancellation.Token)
                .ConfigureAwait(false);

            if (result is not UpdateAvailable update)
            {
                return;
            }

            try
            {
                application.Invoke(() =>
                {
                    availableUpdate = update.Update;
                    visibleMainMenu?.ShowAvailableUpdate(update.Update);
                    application.LayoutAndDraw();
                });
            }
            catch (ObjectDisposedException) { }
            catch (InvalidOperationException) { }
        }
    }

    private TerminalMenuItem[] CreateMenuItems(TerminalTaskRunner taskRunner)
    {
        return
        [
            new TerminalMenuItem(
                LocalizedStrings.MainMenu_InstallMod_Title,
                LocalizedStrings.MainMenu_InstallMod_Description,
                returnToMainMenu => new InstallModView(
                    _workflowService, _settings, taskRunner, returnToMainMenu)),
            new TerminalMenuItem(
                LocalizedStrings.MainMenu_UninstallMod_Title,
                LocalizedStrings.MainMenu_UninstallMod_Description,
                returnToMainMenu => new UninstallModView(_workflowService, taskRunner, returnToMainMenu)),
            new TerminalMenuItem(
                LocalizedStrings.MainMenu_InspectAssets_Title,
                LocalizedStrings.MainMenu_InspectAssets_Description,
                returnToMainMenu => new InspectAssetsView(_workflowService, taskRunner, returnToMainMenu)),
            new TerminalMenuItem(
                LocalizedStrings.MainMenu_Settings_Title,
                LocalizedStrings.MainMenu_Settings_Description,
                returnToMainMenu => new SettingsView(_settings, returnToMainMenu, _loggingLevelSwitch)),
        ];
    }
}

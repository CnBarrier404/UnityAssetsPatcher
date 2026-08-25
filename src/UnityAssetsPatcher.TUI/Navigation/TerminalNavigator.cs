using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Terminal.Gui.ViewBase;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Features.Recovery;
using UnityAssetsPatcher.Application.Features.RepositoryManagement;
using UnityAssetsPatcher.Application.Messaging;
using UnityAssetsPatcher.Application.Operations;
using UnityAssetsPatcher.Application.Repository;
using UnityAssetsPatcher.Application.Updates;
using UnityAssetsPatcher.TUI.Localization;
using UnityAssetsPatcher.TUI.Pages;
using UnityAssetsPatcher.TUI.Shell;

namespace UnityAssetsPatcher.TUI.Navigation;

public sealed class TerminalNavigator
{
    private readonly TerminalShellView _shell;
    private readonly LocalizedStrings _strings;
    private readonly IServiceScopeFactory? _scopeFactory;
    private readonly TerminalSettings? _settings;
    private readonly ILoggingLevelSwitch? _loggingLevelSwitch;
    private readonly TerminalTaskRunner? _taskRunner;
    private readonly Func<string?> _pickModFile;
    private readonly Action _requestStop;
    private UpdateInfo? _availableUpdate;
    private MainMenuView? _visibleMainMenu;
    private RepositoryRecoveryReport _recovery = RepositoryRecoveryReport.Clean;

    public TerminalNavigator(TerminalShellView shell, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(shell);
        ArgumentNullException.ThrowIfNull(culture);

        _shell = shell;
        _strings = new LocalizedStrings(culture);
        _pickModFile = static () => null;
        _requestStop = static () => { };
    }

    public TerminalNavigator(
        TerminalShellView shell,
        CultureInfo culture,
        IServiceScopeFactory scopeFactory,
        TerminalSettings settings,
        ILoggingLevelSwitch? loggingLevelSwitch,
        TerminalTaskRunner taskRunner,
        Action requestStop,
        Func<string?> pickModFile)
        : this(shell, culture)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(taskRunner);
        ArgumentNullException.ThrowIfNull(requestStop);
        ArgumentNullException.ThrowIfNull(pickModFile);

        _scopeFactory = scopeFactory;
        _settings = settings;
        _loggingLevelSwitch = loggingLevelSwitch;
        _taskRunner = taskRunner;
        _pickModFile = pickModFile;
        _requestStop = requestStop;
    }

    public void ShowMainMenu()
    {
        var items = CreateMenuItems();
        TerminalUpdateNotice? updateNotice = _availableUpdate is null
            ? null
            : CreateUpdateNotice(_availableUpdate);
        var menu = new MainMenuView(_strings.MainMenu_Title, items, updateNotice);

        _visibleMainMenu = menu;

        menu.ItemSelected += (_, item) =>
        {
            _visibleMainMenu = null;

            View content = item.CreateView(ShowMainMenu);

            _shell.ShowContent(content);
        };

        _shell.ShowContent(menu);
    }

    public void ShowAvailableUpdate(UpdateInfo update)
    {
        ArgumentNullException.ThrowIfNull(update);

        _availableUpdate = update;

        _visibleMainMenu?.ShowAvailableUpdate(CreateUpdateNotice(update));
    }

    public void ShowRepositoryInitializationResult(
        OperationResult<RepositoryRecoveryReport> result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result is OperationFailed<RepositoryRecoveryReport> failed &&
            failed.Error.Code == RepositoryErrorCodes.UnsupportedVersion)
        {
            ShowUnsupportedRepository(failed.Error);

            return;
        }

        _recovery = result switch
        {
            OperationSucceeded<RepositoryRecoveryReport> succeeded => succeeded.Value,
            OperationFailed<RepositoryRecoveryReport> recoveryFailed =>
                recoveryFailed.Error.Recovery ?? FailedRecovery(),
            _ => throw new ArgumentOutOfRangeException(nameof(result))
        };

        ShowRecoveryResult();
    }

    private TerminalMenuItem[] CreateMenuItems()
    {
        if (_scopeFactory is null || _settings is null || _taskRunner is null)
        {
            return
            [
                CreateEmptyPageMenuItem(
                    _strings.MainMenu_InstallMod_Title,
                    _strings.MainMenu_InstallMod_Description),
                CreateEmptyPageMenuItem(
                    _strings.MainMenu_UninstallMod_Title,
                    _strings.MainMenu_UninstallMod_Description),
                CreateEmptyPageMenuItem(
                    _strings.MainMenu_InspectAssets_Title,
                    _strings.MainMenu_InspectAssets_Description),
                CreateEmptyPageMenuItem(
                    _strings.MainMenu_Settings_Title,
                    _strings.MainMenu_Settings_Description)
            ];
        }

        return
        [
            new TerminalMenuItem(
                _strings.MainMenu_InstallMod_Title,
                _strings.MainMenu_InstallMod_Description,
                returnToMainMenu => new InstallModView(
                    _strings,
                    _scopeFactory,
                    _settings,
                    _taskRunner,
                    _pickModFile,
                    returnToMainMenu)),
            new TerminalMenuItem(
                _strings.MainMenu_UninstallMod_Title,
                _strings.MainMenu_UninstallMod_Description,
                returnToMainMenu => new UninstallModView(
                    _strings,
                    _scopeFactory,
                    _taskRunner,
                    returnToMainMenu)),
            new TerminalMenuItem(
                _strings.MainMenu_InspectAssets_Title,
                _strings.MainMenu_InspectAssets_Description,
                returnToMainMenu => new InspectAssetsView(
                    _strings,
                    _scopeFactory,
                    _taskRunner,
                    returnToMainMenu)),
            new TerminalMenuItem(
                _strings.MainMenu_Settings_Title,
                _strings.MainMenu_Settings_Description,
                returnToMainMenu => new SettingsView(
                    _strings,
                    _settings,
                    returnToMainMenu,
                    _loggingLevelSwitch))
        ];
    }

    private void PreviewRecovery(string gameDirectory)
    {
        bool started = _taskRunner!.TryRun(
            () => DispatchAsync<PreviewRecoveryRequest, OperationResult<RepositoryRecoveryPreview>>(
                new PreviewRecoveryRequest(gameDirectory)),
            result =>
            {
                if (result is not OperationSucceeded<RepositoryRecoveryPreview> succeeded)
                {
                    ShowRecoveryFailure();

                    return;
                }

                RepositoryRecoveryPreview preview = succeeded.Value;

                if (!preview.CanRecover)
                {
                    _recovery = new RepositoryRecoveryReport(preview.Status, [], preview.Issues);
                    ShowRecoveryResult();

                    return;
                }

                _shell.ShowContent(new RepositoryRecoveryPreviewView(
                    _strings,
                    preview,
                    () => Recover(preview.GameDirectory!),
                    ShowRecoveryResult,
                    _requestStop));
            },
            _ => ShowRecoveryFailure());

        if (!started)
        {
            ShowRecoveryFailure();
        }
    }

    private void RetryRepositoryInitialization()
    {
        bool started = _taskRunner!.TryRun(
            () => Task.FromResult(InitializeRepository()),
            ShowRepositoryInitializationResult,
            _ => ShowRecoveryFailure());

        if (!started)
        {
            ShowRecoveryFailure();
        }
    }

    private OperationResult<RepositoryRecoveryReport> InitializeRepository()
    {
        using IServiceScope scope = _scopeFactory!.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRepository>();

        return new RepositoryInitializationModule(repository).Initialize();
    }

    private void Recover(string gameDirectory)
    {
        RunRecoveryOperation(() => DispatchAsync<RecoverRecoveryRequest, OperationResult<RepositoryRecoveryReport>>(
            new RecoverRecoveryRequest(gameDirectory)));
    }

    private void ShowUnsupportedRepository(OperationError formatError, OperationError? clearError = null)
    {
        string actualVersion = ParameterText(formatError, "actual") ?? "?";
        string supportedVersion = ParameterText(formatError, "supported") ??
                                  RepositoryService.CurrentRepositoryFormatVersion.ToString(
                                      CultureInfo.InvariantCulture);
        string? failure = clearError is null ? null : OperationErrorFormatter.Format(_strings, clearError);

        _shell.ShowContent(new UnsupportedRepositoryView(
            _strings,
            actualVersion,
            supportedVersion,
            failure,
            () => ShowClearUnsupportedRepositoryConfirmation(formatError),
            _requestStop));
    }

    private void ShowClearUnsupportedRepositoryConfirmation(OperationError formatError)
    {
        _shell.ShowContent(new ClearUnsupportedRepositoryConfirmationView(
            _strings,
            () => ClearUnsupportedRepository(formatError),
            () => ShowUnsupportedRepository(formatError)));
    }

    private void ClearUnsupportedRepository(OperationError formatError)
    {
        bool started = _taskRunner!.TryRun(
            () => DispatchAsync<ClearUnsupportedRepositoryRequest, OperationResult<RepositoryClearResult>>(
                new ClearUnsupportedRepositoryRequest()),
            result =>
            {
                if (result is OperationSucceeded<RepositoryClearResult>)
                {
                    ShowMainMenu();

                    return;
                }

                var failed = (OperationFailed<RepositoryClearResult>)result;
                ShowUnsupportedRepository(formatError, failed.Error);
            },
            _ => ShowUnsupportedRepository(
                formatError,
                new OperationError(RepositoryErrorCodes.Unsafe)));

        if (!started)
        {
            ShowUnsupportedRepository(
                formatError,
                new OperationError(RepositoryErrorCodes.OperationAlreadyRunning));
        }
    }

    private void RunRecoveryOperation(Func<Task<OperationResult<RepositoryRecoveryReport>>> operation)
    {
        bool started = _taskRunner!.TryRun(
            operation,
            result =>
            {
                _recovery = result switch
                {
                    OperationSucceeded<RepositoryRecoveryReport> succeeded => succeeded.Value,
                    OperationFailed<RepositoryRecoveryReport> failed => failed.Error.Recovery ?? FailedRecovery(),
                    _ => throw new ArgumentOutOfRangeException(nameof(result))
                };

                ShowRecoveryResult();
            },
            _ => ShowRecoveryFailure());

        if (!started)
        {
            ShowRecoveryFailure();
        }
    }

    private void ShowRecoveryFailure()
    {
        _recovery = FailedRecovery();
        ShowRecoveryResult();
    }

    private void ShowRecoveryResult()
    {
        if (_recovery.Status is RepositoryRecoveryStatus.RecoveryRequired or RepositoryRecoveryStatus.Locked)
        {
            _shell.ShowContent(new RepositoryRecoveryView(
                _strings,
                _recovery,
                PreviewRecovery,
                RetryRepositoryInitialization,
                _requestStop));

            return;
        }

        ShowMainMenu();
    }

    private static RepositoryRecoveryReport FailedRecovery()
    {
        return new RepositoryRecoveryReport(
            RepositoryRecoveryStatus.Locked,
            [],
            [new RepositoryRecoveryIssue(RepositoryRecoveryIssueCode.UnexpectedFailure, string.Empty)]);
    }

    private static string? ParameterText(OperationError error, string key)
    {
        return error.Parameters.TryGetValue(key, out object? value)
            ? Convert.ToString(value, CultureInfo.InvariantCulture)
            : null;
    }

    private async Task<TResponse> DispatchAsync<TRequest, TResponse>(TRequest request)
        where TRequest : IRequest<TResponse>
    {
        using IServiceScope scope = _scopeFactory!.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IRequestDispatcher>();

        return await dispatcher.DispatchAsync<TRequest, TResponse>(request).ConfigureAwait(false);
    }

    private TerminalMenuItem CreateEmptyPageMenuItem(string title, string description)
    {
        return new TerminalMenuItem(
            title,
            description,
            returnToMainMenu => new EmptyPageView(title, _strings.EmptyPage_BackAction, returnToMainMenu));
    }

    private TerminalUpdateNotice CreateUpdateNotice(UpdateInfo update)
    {
        return new TerminalUpdateNotice(
            _strings.Update_AvailableFormat(update.Version),
            _strings.Update_DownloadFormat(update.ReleaseUrl));
    }
}

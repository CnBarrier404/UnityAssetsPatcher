using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Terminal.Gui.ViewBase;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Features.Recovery;
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
    private readonly Action _requestStop;
    private AvailableUpdate? _availableUpdate;
    private MainMenuView? _visibleMainMenu;
    private RepositoryRecoveryReport _recovery = RepositoryRecoveryReport.Clean;

    public TerminalNavigator(TerminalShellView shell, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(shell);
        ArgumentNullException.ThrowIfNull(culture);

        _shell = shell;
        _strings = new LocalizedStrings(culture);
        _requestStop = static () => { };
    }

    public TerminalNavigator(
        TerminalShellView shell,
        CultureInfo culture,
        IServiceScopeFactory scopeFactory,
        TerminalSettings settings,
        ILoggingLevelSwitch? loggingLevelSwitch,
        TerminalTaskRunner taskRunner,
        Action requestStop)
        : this(shell, culture)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(taskRunner);
        ArgumentNullException.ThrowIfNull(requestStop);

        _scopeFactory = scopeFactory;
        _settings = settings;
        _loggingLevelSwitch = loggingLevelSwitch;
        _taskRunner = taskRunner;
        _requestStop = requestStop;
    }

    public void Start()
    {
        if (_scopeFactory is null || _taskRunner is null)
        {
            ShowMainMenu();

            return;
        }

        CheckRecovery();
    }

    public void ShowMainMenu()
    {
        TerminalMenuItem[] items = CreateMenuItems();
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

    public void ShowAvailableUpdate(AvailableUpdate update)
    {
        ArgumentNullException.ThrowIfNull(update);

        _availableUpdate = update;

        _visibleMainMenu?.ShowAvailableUpdate(CreateUpdateNotice(update));
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
                    _strings.MainMenu_Settings_Description),
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
                    _loggingLevelSwitch)),
        ];
    }

    private void CheckRecovery()
    {
        RunRecoveryOperation(() => Task.FromResult(InitializeRepository()));
    }

    private OperationResult<RepositoryRecoveryReport> InitializeRepository()
    {
        try
        {
            using IServiceScope scope = _scopeFactory!.CreateScope();
            scope.ServiceProvider.GetRequiredService<IRepository>().Initialize();

            return new OperationSucceeded<RepositoryRecoveryReport>(RepositoryRecoveryReport.Clean);
        }
        catch (RepositoryRecoveryException exception)
        {
            var error = new OperationError(
                RepositoryErrorCodes.RecoveryRequired,
                recovery: exception.Recovery);

            return new OperationFailed<RepositoryRecoveryReport>(error);
        }
        catch (LegacyRepositoryWriteException exception)
        {
            var error = new OperationError(
                RepositoryErrorCodes.UnsupportedVersion,
                new Dictionary<string, object?> { ["detail"] = exception.Message });

            return new OperationFailed<RepositoryRecoveryReport>(error);
        }
        catch (NotSupportedException exception)
        {
            var error = new OperationError(
                RepositoryErrorCodes.UnsupportedVersion,
                new Dictionary<string, object?> { ["detail"] = exception.Message });

            return new OperationFailed<RepositoryRecoveryReport>(error);
        }
        catch (InvalidOperationException exception)
        {
            OperationErrorCode code = exception.InnerException is IOException
                ? RepositoryErrorCodes.OperationAlreadyRunning
                : RepositoryErrorCodes.Unsafe;
            var error = new OperationError(
                code,
                new Dictionary<string, object?> { ["detail"] = exception.Message });

            return new OperationFailed<RepositoryRecoveryReport>(error);
        }
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

    private void Recover(string gameDirectory)
    {
        RunRecoveryOperation(() => DispatchAsync<RecoverRecoveryRequest, OperationResult<RepositoryRecoveryReport>>(
            new RecoverRecoveryRequest(gameDirectory)));
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
                    _ => throw new ArgumentOutOfRangeException(nameof(result)),
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
                CheckRecovery,
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

    private async Task<TResponse> DispatchAsync<TRequest, TResponse>(TRequest request)
        where TRequest : IRequest<TResponse>
    {
        using IServiceScope scope = _scopeFactory!.CreateScope();
        IRequestDispatcher dispatcher = scope.ServiceProvider.GetRequiredService<IRequestDispatcher>();

        return await dispatcher.DispatchAsync<TRequest, TResponse>(request).ConfigureAwait(false);
    }

    private TerminalMenuItem CreateEmptyPageMenuItem(string title, string description)
    {
        return new TerminalMenuItem(
            title,
            description,
            returnToMainMenu => new EmptyPageView(title, _strings.EmptyPage_BackAction, returnToMainMenu));
    }

    private TerminalUpdateNotice CreateUpdateNotice(AvailableUpdate update)
    {
        return new TerminalUpdateNotice(
            _strings.Update_AvailableFormat(update.Version),
            _strings.Update_DownloadFormat(update.ReleaseUrl));
    }
}

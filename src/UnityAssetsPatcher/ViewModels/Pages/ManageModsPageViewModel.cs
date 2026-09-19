using System.Collections.ObjectModel;
using Microsoft.Extensions.DependencyInjection;
using RentADeveloper.ResXLocalization;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Features.Uninstall;
using UnityAssetsPatcher.Application.Installation;
using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Application.Messaging;
using UnityAssetsPatcher.Application.Operations;
using UnityAssetsPatcher.Application.Repository;
using UnityAssetsPatcher.Localization;
using UnityAssetsPatcher.Notifications;

namespace UnityAssetsPatcher.ViewModels.Pages;

public sealed class ManageModsPageViewModel : ViewModelBase
{
    public ObservableCollection<InstalledModViewModel> Mods { get; } = [];
    public bool IsBusy { get; private set; }
    public bool HasLoadFailed { get; private set; }
    public string BusyMessage { get; private set; } = string.Empty;

    public bool IsNotBusy => !IsBusy;
    public bool IsEmpty => _hasLoaded && Mods.Count == 0 && !IsBusy && !HasLoadFailed;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly INotificationService _notifications;
    private bool _hasLoaded;

    public ManageModsPageViewModel(IServiceScopeFactory scopeFactory, INotificationService notifications)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));
    }

    public async Task RefreshAsync()
    {
        if (IsBusy)
        {
            return;
        }

        SetBusy(true, StringsKeys.ManageModsPage_Loading);
        try
        {
            await LoadModsAsync();
        }
        finally
        {
            SetBusy(false);
        }
    }

    public async Task UninstallAsync(InstalledModViewModel mod)
    {
        ArgumentNullException.ThrowIfNull(mod);
        if (IsBusy || !Mods.Contains(mod))
        {
            return;
        }

        SetBusy(true, StringsKeys.ManageModsPage_CheckingUninstall);
        try
        {
            var previewResult = await DispatchAsync<UninstallPreviewRequest, OperationResult<UninstallPreviewResult>>(
                new UninstallPreviewRequest(mod.InstallId));
            if (previewResult is OperationFailed<UninstallPreviewResult> previewFailure)
            {
                ShowFailure(StringsKeys.ManageModsPage_UninstallFailedTitle, previewFailure.Error.Code);
                return;
            }

            if (previewResult is not OperationSucceeded<UninstallPreviewResult> previewSuccess)
            {
                throw new InvalidOperationException("The uninstall preview returned an unknown result.");
            }

            UninstallPreviewResult preview = previewSuccess.Value;
            if (!preview.CanUninstall)
            {
                string message = preview.DependencyFailures.Count > 0
                    ? Localizer.Current.Get(StringsKeys.ManageModsPage_DependencyFailure) + Environment.NewLine +
                      string.Join(Environment.NewLine, preview.DependencyFailures
                          .Select(failure => $"{failure.ModName} {failure.ModVersion}").Distinct())
                    : Localizer.Current.Get(StringsKeys.ManageModsPage_FilesChanged);
                _notifications.Show(
                    Localizer.Current.Get(StringsKeys.ManageModsPage_UninstallFailedTitle),
                    message,
                    NotificationKind.Error);
                return;
            }

            SetBusy(true, StringsKeys.ManageModsPage_Uninstalling);
            var result = await DispatchAsync<UninstallModRequest, OperationResult<UninstallModResult>>(
                new UninstallModRequest(preview.InstallId, preview.GameDirectory));
            switch (result)
            {
                case OperationSucceeded<UninstallModResult> succeeded:
                    Mods.Remove(mod);
                    _notifications.Show(
                        Localizer.Current.Get(StringsKeys.ManageModsPage_UninstallSucceededTitle),
                        $"{succeeded.Value.ModName} {succeeded.Value.ModVersion}",
                        NotificationKind.Success);
                    SetBusy(true, StringsKeys.ManageModsPage_Loading);
                    await LoadModsAsync();
                    break;
                case OperationFailed<UninstallModResult> failed:
                    ShowFailure(StringsKeys.ManageModsPage_UninstallFailedTitle, failed.Error.Code);
                    break;
                default:
                    throw new InvalidOperationException("The uninstall operation returned an unknown result.");
            }
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task LoadModsAsync()
    {
        var result =
            await DispatchAsync<ListInstalledModsRequest, OperationResult<IReadOnlyList<InstallRecordSummary>>>(
                new ListInstalledModsRequest());
        switch (result)
        {
            case OperationSucceeded<IReadOnlyList<InstallRecordSummary>> succeeded:
                Mods.Clear();
                foreach (InstallRecordSummary record in succeeded.Value.OrderByDescending(record => record.InstalledAt))
                {
                    Mods.Add(new InstalledModViewModel(record));
                }

                _hasLoaded = true;
                HasLoadFailed = false;
                break;
            case OperationFailed<IReadOnlyList<InstallRecordSummary>> failed:
                HasLoadFailed = true;
                ShowFailure(StringsKeys.ManageModsPage_RefreshFailedTitle, failed.Error.Code);
                break;
            default:
                throw new InvalidOperationException("The installed mod query returned an unknown result.");
        }

        OnPropertyChanged(nameof(HasLoadFailed));
        OnPropertyChanged(nameof(IsEmpty));
    }

    private Task<TResponse> DispatchAsync<TRequest, TResponse>(TRequest request)
        where TRequest : IRequest<TResponse>
    {
        // Repository queries and composition include synchronous file operations.
        return Task.Run(async () =>
        {
            await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
            var dispatcher = scope.ServiceProvider.GetRequiredService<IRequestDispatcher>();
            return await dispatcher.DispatchAsync<TRequest, TResponse>(request, CancellationToken.None);
        });
    }

    private void SetBusy(bool value, ResourceKey? messageKey = null)
    {
        IsBusy = value;
        BusyMessage = messageKey is { } key ? Localizer.Current.Get(key) : string.Empty;
        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(IsNotBusy));
        OnPropertyChanged(nameof(BusyMessage));
        OnPropertyChanged(nameof(IsEmpty));
    }

    private void ShowFailure(ResourceKey title, OperationErrorCode code)
    {
        ResourceKey message = code switch
        {
            _ when code == RepositoryErrorCodes.OperationAlreadyRunning => StringsKeys.ManageModsPage_OperationLocked,
            _ when code == RepositoryErrorCodes.RecoveryRequired => StringsKeys.ManageModsPage_RecoveryRequired,
            _ when code == RepositoryErrorCodes.UnsupportedVersion => StringsKeys.ManageModsPage_UnsupportedRepository,
            _ when code == ModOperationErrorCodes.InstallRecordNotFound => StringsKeys.ManageModsPage_RecordMissing,
            _ when code == ModOperationErrorCodes.FileIntegrityMismatch || code == RepositoryErrorCodes.Unsafe =>
                StringsKeys.ManageModsPage_IntegrityFailure,
            _ when code == FileErrorCodes.AccessDenied => StringsKeys.ManageModsPage_AccessDenied,
            _ when code == GameDirectoryErrorCodes.Required || code == GameDirectoryErrorCodes.NotFound =>
                StringsKeys.ManageModsPage_GameDirectoryMissing,
            _ => StringsKeys.ManageModsPage_OperationFailed
        };
        _notifications.Show(Localizer.Current.Get(title), Localizer.Current.Get(message), NotificationKind.Error);
    }
}

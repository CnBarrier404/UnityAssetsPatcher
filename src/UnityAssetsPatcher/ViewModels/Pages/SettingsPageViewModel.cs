using System.Diagnostics;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using UnityAssetsPatcher.Application;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Operations;
using UnityAssetsPatcher.Application.Updates;
using UnityAssetsPatcher.Localization;
using UnityAssetsPatcher.Notifications;
using RentADeveloper.ResXLocalization;

namespace UnityAssetsPatcher.ViewModels.Pages;

public sealed class SettingsPageViewModel : ViewModelBase
{
    public static string LogDirectory => AppConfig.LogDirectory;

    public ICommand OpenLogDirectoryCommand { get; }
    public bool IsCheckingForUpdates { get; private set; }
    public bool CanCheckForUpdates => !IsCheckingForUpdates;

    public bool VerboseLogging
    {
        get => _runtimeConfig.VerboseLogging;
        set
        {
            if (_runtimeConfig.VerboseLogging == value)
            {
                return;
            }

            _runtimeConfig.VerboseLogging = value;
            _loggingLevelSwitch.MinimumLevel = value ? LoggingLevel.Debug : LoggingLevel.Information;
            OnPropertyChanged();
        }
    }

    private readonly AppRuntimeConfig _runtimeConfig;
    private readonly ILoggingLevelSwitch _loggingLevelSwitch;
    private readonly UpdateCheckModule _updates;
    private readonly INotificationService _notifications;

    public SettingsPageViewModel(AppRuntimeConfig runtimeConfig, ILoggingLevelSwitch loggingLevelSwitch,
        UpdateCheckModule updates, INotificationService notifications)
    {
        _runtimeConfig = runtimeConfig ?? throw new ArgumentNullException(nameof(runtimeConfig));
        _loggingLevelSwitch = loggingLevelSwitch ?? throw new ArgumentNullException(nameof(loggingLevelSwitch));
        _updates = updates ?? throw new ArgumentNullException(nameof(updates));
        _notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));

        _loggingLevelSwitch.MinimumLevel = _runtimeConfig.VerboseLogging
            ? LoggingLevel.Debug
            : LoggingLevel.Information;

        OpenLogDirectoryCommand = new RelayCommand(OpenLogDirectory);
    }

    public async Task CheckForUpdatesAsync(bool startup, Func<UpdateInfo, Task<bool>> confirmAsync,
        CancellationToken cancellationToken)
    {
        if (IsCheckingForUpdates)
        {
            return;
        }

        SetChecking(true);
        try
        {
            var result = await _updates.CheckForUpdateAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (result is OperationFailed<UpdateInfo?>)
            {
                if (!startup)
                {
                    Notify(StringsKeys.Updates_CheckFailed, NotificationKind.Warning);
                }

                return;
            }

            if (result is not OperationSucceeded<UpdateInfo?> success)
            {
                throw new InvalidOperationException("The update check returned an unknown result.");
            }

            if (success.Value is not { } update)
            {
                if (!startup)
                {
                    Notify(StringsKeys.Updates_NoUpdate, NotificationKind.Success);
                }

                return;
            }

            if (await confirmAsync(update))
            {
                cancellationToken.ThrowIfCancellationRequested();
                Process.Start(new ProcessStartInfo(update.ReleaseUrl.AbsoluteUri) { UseShellExecute = true });
            }
        }
        finally
        {
            SetChecking(false);
        }
    }

    private void Notify(ResourceKey messageKey, NotificationKind kind = NotificationKind.Information)
    {
        _notifications.Show(Localizer.Current.Get(StringsKeys.SettingsPage_UpdatesSectionTitle),
            Localizer.Current.Get(messageKey), kind);
    }

    private void SetChecking(bool value)
    {
        IsCheckingForUpdates = value;
        OnPropertyChanged(nameof(IsCheckingForUpdates));
        OnPropertyChanged(nameof(CanCheckForUpdates));
    }

    private void OpenLogDirectory()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = LogDirectory,
            UseShellExecute = true
        });
    }
}

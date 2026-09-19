using Microsoft.Extensions.DependencyInjection;
using UnityAssetsPatcher.Application;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Localization;
using UnityAssetsPatcher.Notifications;
using UnityAssetsPatcher.ViewModels.Pages;

namespace UnityAssetsPatcher.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    public static string Title => AppConfig.Name;
    public ViewModelBase CurrentPage => _selectedItem.Page;
    public IReadOnlyList<NavigationViewItemViewModel> MenuItems { get; }
    public NotificationService Notifications { get; }

    private NavigationViewItemViewModel _selectedItem;

    public MainWindowViewModel(
        IServiceScopeFactory scopeFactory,
        NotificationService notifications,
        AppRuntimeConfig runtimeConfig,
        ILoggingLevelSwitch loggingLevelSwitch)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        Notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));
        ArgumentNullException.ThrowIfNull(runtimeConfig);
        ArgumentNullException.ThrowIfNull(loggingLevelSwitch);

        MenuItems =
        [
            new NavigationViewItemViewModel(
                StringsKeys.MainMenu_InstallMod_Title,
                new InstallModPageViewModel(scopeFactory, notifications)),
            new NavigationViewItemViewModel(StringsKeys.Navigation_ManageMods, new ManageModsPageViewModel()),
            new NavigationViewItemViewModel(StringsKeys.MainMenu_Settings_Title,
                new SettingsPageViewModel(runtimeConfig, loggingLevelSwitch))
        ];

        _selectedItem = MenuItems[0];
    }

    public NavigationViewItemViewModel? SelectedMenuItem
    {
        get => _selectedItem;
        set
        {
            if (value is null)
            {
                OnPropertyChanged();
            }
            else
            {
                SelectItem(value);
            }
        }
    }

    private void SelectItem(NavigationViewItemViewModel item)
    {
        if (ReferenceEquals(_selectedItem, item))
        {
            return;
        }

        _selectedItem = item;

        OnPropertyChanged(nameof(SelectedMenuItem));
        OnPropertyChanged(nameof(CurrentPage));
    }
}

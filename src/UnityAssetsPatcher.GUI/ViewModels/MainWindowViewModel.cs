using Microsoft.Extensions.DependencyInjection;
using UnityAssetsPatcher.Application;
using UnityAssetsPatcher.GUI.Localization;
using UnityAssetsPatcher.GUI.ViewModels.Pages;

namespace UnityAssetsPatcher.GUI.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    public static string Title => AppConfig.Name;
    public ViewModelBase CurrentPage => _selectedItem.Page;
    public IReadOnlyList<NavigationViewItemViewModel> MenuItems { get; }
    public IReadOnlyList<NavigationViewItemViewModel> FooterMenuItems { get; }

    private NavigationViewItemViewModel _selectedItem;

    public MainWindowViewModel(IServiceScopeFactory? scopeFactory = null)
    {
        MenuItems =
        [
            new NavigationViewItemViewModel(
                StringsKeys.MainMenu_InstallMod_Title,
                new InstallModPageViewModel(scopeFactory)),
            new NavigationViewItemViewModel(StringsKeys.Navigation_ManageMods, new ManageModsPageViewModel()),
            new NavigationViewItemViewModel(StringsKeys.MainMenu_Settings_Title, new SettingsPageViewModel())
        ];

        FooterMenuItems =
        [
            new NavigationViewItemViewModel(StringsKeys.Navigation_About, new AboutPageViewModel(), true)
        ];

        _selectedItem = MenuItems[0];
    }

    public NavigationViewItemViewModel? SelectedMenuItem
    {
        get => _selectedItem.IsFooterItem ? null : _selectedItem;
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

    public NavigationViewItemViewModel? SelectedFooterMenuItem
    {
        get => _selectedItem.IsFooterItem ? _selectedItem : null;
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
        OnPropertyChanged(nameof(SelectedFooterMenuItem));
        OnPropertyChanged(nameof(CurrentPage));
    }
}

using Microsoft.Extensions.DependencyInjection;
using UnityAssetsPatcher.Application;
using UnityAssetsPatcher.GUI.Localization;
using UnityAssetsPatcher.GUI.ViewModels.Pages;

namespace UnityAssetsPatcher.GUI.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    public static string Title => AppConfig.Name;
    public ViewModelBase CurrentPage => _selected.Page;
    public IReadOnlyList<NavigationItemViewModel> PrimaryItems { get; }
    public IReadOnlyList<NavigationItemViewModel> PinnedItems { get; }

    private NavigationItemViewModel _selected;

    public MainWindowViewModel(IServiceScopeFactory? scopeFactory = null)
    {
        PrimaryItems =
        [
            new NavigationItemViewModel(
                StringsKeys.MainMenu_InstallMod_Title,
                new InstallModPageViewModel(scopeFactory)),
            new NavigationItemViewModel(StringsKeys.Navigation_ManageMods, new ManageModsPageViewModel()),
            new NavigationItemViewModel(StringsKeys.MainMenu_Settings_Title, new SettingsPageViewModel())
        ];

        PinnedItems =
        [
            new NavigationItemViewModel(StringsKeys.Navigation_About, new AboutPageViewModel(), true)
        ];

        _selected = PrimaryItems[0];
    }

    public NavigationItemViewModel? PrimarySelection
    {
        get => _selected.IsPinned ? null : _selected;
        set
        {
            if (value is null)
            {
                OnPropertyChanged();
            }
            else
            {
                Select(value);
            }
        }
    }

    public NavigationItemViewModel? PinnedSelection
    {
        get => _selected.IsPinned ? _selected : null;
        set
        {
            if (value is null)
            {
                OnPropertyChanged();
            }
            else
            {
                Select(value);
            }
        }
    }

    private void Select(NavigationItemViewModel item)
    {
        if (ReferenceEquals(_selected, item))
        {
            return;
        }

        _selected = item;

        OnPropertyChanged(nameof(PrimarySelection));
        OnPropertyChanged(nameof(PinnedSelection));
        OnPropertyChanged(nameof(CurrentPage));
    }
}

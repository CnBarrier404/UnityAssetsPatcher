using Avalonia.Controls;
using Avalonia.VisualTree;
using Avalonia.Interactivity;
using RentADeveloper.ResXLocalization;
using UnityAssetsPatcher.Localization;
using UnityAssetsPatcher.ViewModels.Pages;

namespace UnityAssetsPatcher.Views;

public partial class ManageModsPage : UserControl
{
    public ManageModsPage()
    {
        InitializeComponent();
    }

    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ManageModsPageViewModel viewModel)
        {
            await viewModel.RefreshAsync();
        }
    }

    private async void OnRefreshClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (DataContext is ManageModsPageViewModel viewModel)
        {
            await viewModel.RefreshAsync();
        }
    }

    private async void OnUninstallClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (DataContext is ManageModsPageViewModel viewModel &&
            sender is Control { DataContext: InstalledModViewModel mod } button &&
            TopLevel.GetTopLevel(this) is MainWindow window)
        {
            await viewModel.UninstallAsync(mod, preview => window.ShowConfirmationAsync(
                Localizer.Current.Get(StringsKeys.ManageModsPage_ConfirmUninstallTitle),
                string.Format(System.Globalization.CultureInfo.CurrentCulture,
                    Localizer.Current.Get(StringsKeys.ManageModsPage_ConfirmUninstallMessage),
                    preview.ModName, preview.ModVersion),
                Localizer.Current.Get(StringsKeys.ManageModsPage_UninstallButton),
                Localizer.Current.Get(StringsKeys.ContentDialog_Cancel)));
            if (button.IsAttachedToVisualTree())
            {
                button.Focus();
            }
        }
    }
}

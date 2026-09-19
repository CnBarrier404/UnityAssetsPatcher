using Avalonia.Controls;
using Avalonia.Interactivity;
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
            sender is Control { DataContext: InstalledModViewModel mod })
        {
            await viewModel.UninstallAsync(mod);
        }
    }
}

using Avalonia.Controls;
using Avalonia.Interactivity;

namespace UnityAssetsPatcher.Views;

public partial class SettingsPage : UserControl
{
    public SettingsPage()
    {
        InitializeComponent();
    }

    private async void OnCheckForUpdatesClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (TopLevel.GetTopLevel(this) is MainWindow window)
        {
            await window.CheckForUpdatesAsync(false);
        }
    }
}

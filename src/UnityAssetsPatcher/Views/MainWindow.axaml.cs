using Avalonia.Controls;

namespace UnityAssetsPatcher.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    public Task<bool> ShowConfirmationAsync(string title, string message, string primaryText, string closeText)
    {
        if (!IsVisible)
        {
            return Task.FromResult(false);
        }

        return new ContentDialog().ShowAsync(DialogHost, MainContent, title, message, primaryText, closeText);
    }
}

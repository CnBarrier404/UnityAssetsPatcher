using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using UnityAssetsPatcher.GUI.ViewModels;
using UnityAssetsPatcher.GUI.Views;

namespace UnityAssetsPatcher.GUI;

public partial class App : Avalonia.Application
{
    private readonly MainWindowViewModel _mainWindowViewModel;

    public App() : this(new MainWindowViewModel()) { }

    public App(MainWindowViewModel mainWindowViewModel)
    {
        _mainWindowViewModel = mainWindowViewModel;
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = _mainWindowViewModel
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}

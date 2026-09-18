using System.Globalization;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using RentADeveloper.ResXLocalization;
using UnityAssetsPatcher.ViewModels;
using UnityAssetsPatcher.Views;

namespace UnityAssetsPatcher;

public partial class App : Avalonia.Application
{
    private readonly MainWindowViewModel? _mainWindowViewModel;

    public App() { }

    public App(MainWindowViewModel mainWindowViewModel)
    {
        _mainWindowViewModel = mainWindowViewModel;
    }

    public override void Initialize()
    {
        Localizer.Current.CurrentCulture = CultureInfo.CurrentUICulture;
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = _mainWindowViewModel ??
                              throw new InvalidOperationException(
                                  "The application must be created by dependency injection.")
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}

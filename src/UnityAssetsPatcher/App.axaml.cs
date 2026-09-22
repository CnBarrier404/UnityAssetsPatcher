using System.Globalization;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using RentADeveloper.ResXLocalization;
using UnityAssetsPatcher.Application.Updates;
using UnityAssetsPatcher.Notifications;
using UnityAssetsPatcher.ViewModels;
using UnityAssetsPatcher.Views;

namespace UnityAssetsPatcher;

public partial class App : Avalonia.Application
{
    private readonly MainWindowViewModel? _mainWindowViewModel;
    private readonly UpdateCheckService? _updateCheckService;
    private readonly INotificationService? _notifications;

    public App() { }

    public App(MainWindowViewModel mainWindowViewModel, UpdateCheckService updateCheckService,
        INotificationService notifications)
    {
        _mainWindowViewModel = mainWindowViewModel ?? throw new ArgumentNullException(nameof(mainWindowViewModel));
        _updateCheckService = updateCheckService ?? throw new ArgumentNullException(nameof(updateCheckService));
        _notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));
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
            desktop.MainWindow = new MainWindow(
                _updateCheckService ?? throw new InvalidOperationException(
                    "The application must be created by dependency injection."),
                _notifications ?? throw new InvalidOperationException(
                    "The application must be created by dependency injection."))
            {
                DataContext = _mainWindowViewModel ??
                              throw new InvalidOperationException(
                                  "The application must be created by dependency injection.")
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}

using System.Diagnostics;
using Avalonia.Controls;
using UnityAssetsPatcher.Controls;
using UnityAssetsPatcher.Application.Updates;
using UnityAssetsPatcher.Notifications;
using UnityAssetsPatcher.ViewModels;
using UnityAssetsPatcher.ViewModels.Pages;
using UnityAssetsPatcher.Localization;
using RentADeveloper.ResXLocalization;

namespace UnityAssetsPatcher.Views;

public partial class MainWindow : Window
{
    private readonly UpdateCheckService? _updateCheckService;
    private readonly INotificationService? _notifications;
    private readonly CancellationTokenSource _updateLifetime = new();
    private Task _updateTask = Task.CompletedTask;
    private bool _startupChecked;
    private bool _closing;

    internal UpdateCheckService UpdateCheckService => _updateCheckService ?? throw new InvalidOperationException(
        "The main window must be created by dependency injection.");

    internal INotificationService Notifications => _notifications ?? throw new InvalidOperationException(
        "The main window must be created by dependency injection.");

    internal bool IsClosing => _closing;

    public MainWindow()
    {
        InitializeComponent();
        InitializeLifetime();
    }

    public MainWindow(UpdateCheckService updateCheckService, INotificationService notifications)
    {
        _updateCheckService = updateCheckService ?? throw new ArgumentNullException(nameof(updateCheckService));
        _notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));

        InitializeComponent();
        InitializeLifetime();
    }

    private void InitializeLifetime()
    {
        Opened += OnOpened;
        Closing += OnClosing;
        Closed += (_, _) => _updateLifetime.Dispose();
    }

    public Task<bool> ShowConfirmationAsync(string title, string message, string primaryText, string closeText,
        CancellationToken cancellationToken = default)
    {
        if (!IsVisible)
        {
            return Task.FromResult(false);
        }

        return new ContentDialog().ShowAsync(DialogHost, MainContent, title, message, primaryText, closeText,
            cancellationToken);
    }

    private Task CheckForUpdateAsync()
    {
        if (_closing || !_updateTask.IsCompleted || DataContext is not MainWindowViewModel viewModel ||
            !viewModel.Settings.TryBeginUpdateCheck())
        {
            return _updateTask;
        }

        _updateTask = RunUpdateCheckAsync(viewModel.Settings, _updateLifetime.Token);
        return _updateTask;
    }

    private async Task RunUpdateCheckAsync(SettingsPageViewModel settings, CancellationToken cancellationToken)
    {
        try
        {
            UpdateInfo? update = await UpdateCheckService.CheckForUpdateAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            if (update is null || !await ShowConfirmationAsync(
                    Localizer.Current.Get(StringsKeys.Updates_AvailableTitle),
                    string.Format(System.Globalization.CultureInfo.CurrentCulture,
                        Localizer.Current.Get(StringsKeys.Updates_DialogMessage), update.Version),
                    Localizer.Current.Get(StringsKeys.Updates_OpenRelease),
                    Localizer.Current.Get(StringsKeys.ContentDialog_Cancel), cancellationToken))
            {
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();
            Process.Start(new ProcessStartInfo(update.ReleaseUrl.AbsoluteUri) { UseShellExecute = true });
        }
        catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested &&
                                                           exception.CancellationToken == cancellationToken)
        {
            // This window owns the update lifetime and cancels it before closing.
        }
        catch (UpdateCheckException)
        {
            // Startup update failures are intentionally silent.
        }
        finally
        {
            settings.EndUpdateCheck();
        }
    }

    private async void OnOpened(object? sender, EventArgs e)
    {
        if (!_startupChecked)
        {
            _startupChecked = true;
            await CheckForUpdateAsync();
        }
    }

    private async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (!_updateTask.IsCompleted)
        {
            e.Cancel = true;
            await FinishUpdatesAndCloseAsync();
        }
    }

    private async Task FinishUpdatesAndCloseAsync()
    {
        if (_closing)
        {
            return;
        }

        _closing = true;
        _updateLifetime.Cancel();
        await _updateTask;
        Close();
    }
}

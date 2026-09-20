using Avalonia.Controls;
using UnityAssetsPatcher.Controls;
using UnityAssetsPatcher.ViewModels;
using UnityAssetsPatcher.Localization;
using RentADeveloper.ResXLocalization;

namespace UnityAssetsPatcher.Views;

public partial class MainWindow : Window
{
    private readonly CancellationTokenSource _updateLifetime = new();
    private Task _updateTask = Task.CompletedTask;
    private bool _startupChecked;
    private bool _closing;

    public MainWindow()
    {
        InitializeComponent();
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

    public Task CheckForUpdatesAsync(bool startup)
    {
        if (_closing || !_updateTask.IsCompleted || DataContext is not MainWindowViewModel viewModel)
        {
            return _updateTask;
        }

        _updateTask = RunUpdateCheckAsync(viewModel, startup, _updateLifetime.Token);
        return _updateTask;
    }

    private async Task RunUpdateCheckAsync(MainWindowViewModel viewModel, bool startup,
        CancellationToken cancellationToken)
    {
        try
        {
            await viewModel.Settings.CheckForUpdatesAsync(startup, update => ShowConfirmationAsync(
                Localizer.Current.Get(StringsKeys.Updates_AvailableTitle),
                string.Format(System.Globalization.CultureInfo.CurrentCulture,
                    Localizer.Current.Get(StringsKeys.Updates_DialogMessage), update.Version),
                Localizer.Current.Get(StringsKeys.Updates_OpenRelease),
                Localizer.Current.Get(StringsKeys.ContentDialog_Cancel), cancellationToken), cancellationToken);
        }
        catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested &&
                                                           exception.CancellationToken == cancellationToken)
        {
            // This window owns the update lifetime and cancels it before closing.
        }
    }

    private async void OnOpened(object? sender, EventArgs e)
    {
        if (!_startupChecked)
        {
            _startupChecked = true;
            await CheckForUpdatesAsync(true);
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

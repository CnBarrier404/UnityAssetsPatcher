using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using UnityAssetsPatcher.Application.Updates;
using UnityAssetsPatcher.Localization;
using UnityAssetsPatcher.Notifications;
using UnityAssetsPatcher.ViewModels.Pages;
using RentADeveloper.ResXLocalization;

namespace UnityAssetsPatcher.Views;

public partial class SettingsPage : UserControl
{
    private readonly CancellationTokenSource _updateLifetime = new();
    private Task _updateTask = Task.CompletedTask;
    private MainWindow? _window;

    public SettingsPage()
    {
        InitializeComponent();
    }

    private async void OnCheckForUpdatesClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        await CheckForUpdateAsync();
    }

    private Task CheckForUpdateAsync()
    {
        if (!_updateTask.IsCompleted || TopLevel.GetTopLevel(this) is not MainWindow window || window.IsClosing ||
            DataContext is not SettingsPageViewModel viewModel ||
            !viewModel.TryBeginUpdateCheck())
        {
            return _updateTask;
        }

        AttachToWindow(window);
        _updateTask = RunUpdateCheckAsync(window, viewModel, _updateLifetime.Token);
        return _updateTask;
    }

    private async Task RunUpdateCheckAsync(MainWindow window, SettingsPageViewModel viewModel,
        CancellationToken cancellationToken)
    {
        try
        {
            UpdateInfo? update = await window.UpdateCheckService.CheckForUpdateAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            if (update is null)
            {
                Notify(window, StringsKeys.Updates_NoUpdate, NotificationKind.Success);
                return;
            }

            if (!await window.ShowConfirmationAsync(
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
            // This page owns the manual update lifetime and cancels it when the window closes.
        }
        catch (UpdateCheckException)
        {
            Notify(window, StringsKeys.Updates_CheckFailed, NotificationKind.Warning);
        }
        finally
        {
            viewModel.EndUpdateCheck();
        }
    }

    private void AttachToWindow(MainWindow window)
    {
        if (ReferenceEquals(_window, window))
        {
            return;
        }

        _window = window;
        window.Closing += OnWindowClosing;
        window.Closed += OnWindowClosed;
    }

    private void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        _updateLifetime.Cancel();
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        if (_window is { } window)
        {
            window.Closing -= OnWindowClosing;
            window.Closed -= OnWindowClosed;
            _window = null;
        }

        _updateLifetime.Dispose();
    }

    private void Notify(MainWindow window, ResourceKey messageKey, NotificationKind kind)
    {
        window.Notifications.Show(
            Localizer.Current.Get(StringsKeys.SettingsPage_UpdatesSectionTitle),
            Localizer.Current.Get(messageKey), kind);
    }
}

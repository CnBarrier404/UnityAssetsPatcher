using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using RentADeveloper.ResXLocalization;
using UnityAssetsPatcher.Localization;
using UnityAssetsPatcher.ViewModels.Pages;

namespace UnityAssetsPatcher.Views;

public partial class InstallModPage : UserControl
{
    public InstallModPage()
    {
        InitializeComponent();
    }

    private async void OnSelectFileClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not InstallModPageViewModel viewModel ||
            TopLevel.GetTopLevel(this)?.StorageProvider is not { } storageProvider)
        {
            return;
        }

        e.Handled = true;

        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType(Localizer.Current.Get(StringsKeys.InstallPage_ModPackageFileType))
                {
                    Patterns = ["*.zip"]
                }
            ],
            Title = Localizer.Current.Get(StringsKeys.InstallPage_SelectFileTitle)
        });

        if (files.Count != 1 || files[0].TryGetLocalPath() is not { } path)
        {
            return;
        }

        await viewModel.LoadPackageAsync(path);
    }

    private void OnDragEnter(object? sender, DragEventArgs e)
    {
        UpdateDragState(e);
    }

    private void OnDragLeave(object? sender, DragEventArgs e)
    {
        SetDropZoneState(false);
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        UpdateDragState(e);
        e.Handled = true;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        SetDropZoneState(false);
        e.Handled = true;

        if (DataContext is not InstallModPageViewModel viewModel)
        {
            e.DragEffects = DragDropEffects.None;
            return;
        }

        if (!TryGetZipPath(e.DataTransfer, out string path))
        {
            e.DragEffects = DragDropEffects.None;
            viewModel.ReportInvalidDrop();
            return;
        }

        e.DragEffects = DragDropEffects.Copy;
        await viewModel.LoadPackageAsync(path);
    }

    private async void OnOptionalGroupClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is InstallModPageViewModel viewModel)
        {
            await viewModel.RefreshOptionalGroupsAsync();
        }
    }

    private async void OnChangeGameDirectoryClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not InstallModPageViewModel viewModel ||
            TopLevel.GetTopLevel(this)?.StorageProvider is not { } storageProvider)
        {
            return;
        }

        e.Handled = true;

        var folders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            AllowMultiple = false,
            Title = Localizer.Current.Get(StringsKeys.InstallPage_SelectFolderTitle)
        });

        if (folders.Count == 1 && folders[0].TryGetLocalPath() is { } path)
        {
            await viewModel.SelectGameDirectoryAsync(path);
        }
    }

    private void OnReselectClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        (DataContext as InstallModPageViewModel)?.ResetSelection();
    }

    private void SetDropZoneState(bool isDragOver)
    {
        DropZone.Classes.Set("drag-over", isDragOver);
    }

    private void UpdateDragState(DragEventArgs e)
    {
        e.DragEffects = DragDropEffects.Copy;
        SetDropZoneState(true);
    }

    private static bool TryGetZipPath(IDataTransfer dataTransfer, out string path)
    {
        path = string.Empty;

        if (!dataTransfer.Contains(DataFormat.File))
        {
            return false;
        }

        var items = dataTransfer.TryGetFiles()?.ToArray() ?? [];

        if (items is not [{ } item] || item is not IStorageFile file)
        {
            return false;
        }

        string? localPath = file.TryGetLocalPath();

        if (string.IsNullOrWhiteSpace(localPath) ||
            !string.Equals(Path.GetExtension(file.Name), ".zip", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        path = localPath!;
        return true;
    }
}

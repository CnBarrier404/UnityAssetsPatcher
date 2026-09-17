using System.Collections.ObjectModel;
using Microsoft.Extensions.DependencyInjection;
using RentADeveloper.ResXLocalization;
using UnityAssetsPatcher.Application.Features.Install;
using UnityAssetsPatcher.Application.Messaging;
using UnityAssetsPatcher.Application.Mods;
using UnityAssetsPatcher.Application.Operations;
using UnityAssetsPatcher.GUI.Localization;

namespace UnityAssetsPatcher.GUI.ViewModels.Pages;

public sealed class InstallModPageViewModel : ViewModelBase
{
    private readonly IServiceScopeFactory? _scopeFactory;
    private CancellationTokenSource? _operationCancellation;
    private InstallPreviewResult? _preview;
    private string? _packagePath;
    private string? _requestedGameDirectory;
    private bool _isBusy;
    private string? _errorMessage;

    public ObservableCollection<InstallOptionalGroupViewModel> OptionalGroups { get; } = [];

    public bool IsDropZoneVisible => _preview is null;
    public bool IsPreviewVisible => _preview is not null;

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(IsNotBusy));
            }
        }
    }

    public bool IsNotBusy => !IsBusy;

    public bool HasOptionalGroups => OptionalGroups.Count > 0;

    public string ModName => _preview?.ModName ?? string.Empty;
    public string ModVersion => _preview?.ModVersion ?? string.Empty;
    public string ModAuthor => _preview?.ModAuthor ?? string.Empty;
    public string? ModDescription => _preview?.ModDescription;
    public bool HasModDescription => !string.IsNullOrWhiteSpace(ModDescription);

    public string PackageFileName =>
        string.IsNullOrWhiteSpace(_packagePath) ? string.Empty : Path.GetFileName(_packagePath);

    public string TargetGameName =>
        string.IsNullOrWhiteSpace(_preview?.TargetGameName)
            ? Localizer.Current.Get(StringsKeys.InstallPage_CustomGameDirectory)
            : _preview.TargetGameName!;

    public string TargetGameDirectory => _preview?.TargetGameDirectory ?? string.Empty;
    public string? ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetProperty(ref _errorMessage, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public InstallModPageViewModel(IServiceScopeFactory? scopeFactory = null)
    {
        _scopeFactory = scopeFactory;
    }

    public Task LoadPackageAsync(string packagePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);

        if (!string.Equals(Path.GetExtension(packagePath), ".zip", StringComparison.OrdinalIgnoreCase))
        {
            SetError(StringsKeys.InstallPage_ErrorInvalidFile);
            return Task.CompletedTask;
        }

        _packagePath = packagePath;
        _requestedGameDirectory = null;
        SetPreview(null);
        OptionalGroups.Clear();
        OnPropertyChanged(nameof(HasOptionalGroups));

        return PreviewPackageAsync(
            packagePath,
            null,
            [],
            true);
    }

    public void ReportInvalidPackageSelection()
    {
        SetError(StringsKeys.InstallPage_ErrorInvalidFile);
    }

    public Task SelectGameDirectoryAsync(string gameDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameDirectory);

        if (_preview is null || string.IsNullOrWhiteSpace(_packagePath) || IsBusy)
        {
            return Task.CompletedTask;
        }

        return PreviewPackageAsync(
            _packagePath,
            gameDirectory,
            GetSelectedOptionalGroups(),
            false);
    }

    public Task RefreshOptionalGroupsAsync()
    {
        if (_preview is null || string.IsNullOrWhiteSpace(_packagePath) || IsBusy)
        {
            return Task.CompletedTask;
        }

        return PreviewPackageAsync(
            _packagePath,
            _requestedGameDirectory,
            GetSelectedOptionalGroups(),
            false);
    }

    public void ResetSelection()
    {
        _operationCancellation?.Cancel();
        _operationCancellation = null;

        SetPreview(null);
        _packagePath = null;
        _requestedGameDirectory = null;
        OptionalGroups.Clear();
        OnPropertyChanged(nameof(PackageFileName));
        OnPropertyChanged(nameof(HasOptionalGroups));
        IsBusy = false;
        ErrorMessage = null;
    }

    private async Task PreviewPackageAsync(
        string packagePath,
        string? gameDirectory,
        IReadOnlyList<string> selectedOptionalGroups,
        bool clearPreviewOnFailure)
    {
        if (_scopeFactory is null)
        {
            SetError(StringsKeys.InstallPage_ErrorNoService);
            return;
        }

        CancellationTokenSource operation = BeginOperation();
        CancellationToken cancellationToken = operation.Token;

        try
        {
            var result = await DispatchAsync<PreviewInstallRequest, OperationResult<InstallPreviewResult>>(
                new PreviewInstallRequest(new InstallRequest(packagePath, gameDirectory)
                {
                    SelectedOptionalGroups = selectedOptionalGroups
                }),
                cancellationToken);

            if (!IsCurrentOperation(operation))
            {
                return;
            }

            switch (result)
            {
                case OperationSucceeded<InstallPreviewResult> succeeded:
                    ApplyPreview(succeeded.Value, gameDirectory, selectedOptionalGroups);
                    break;
                case OperationFailed<InstallPreviewResult> failed:
                    if (clearPreviewOnFailure)
                    {
                        SetPreview(null);
                    }

                    SetError(LocalizeError(failed.Error));
                    break;
                default:
                    throw new InvalidOperationException("The install preview returned an unknown result.");
            }
        }
        catch (OperationCanceledException exception)
            when (cancellationToken.IsCancellationRequested && exception.CancellationToken == cancellationToken) { }
        finally
        {
            EndOperation(operation);
        }
    }

    private async Task<TResponse> DispatchAsync<TRequest, TResponse>(
        TRequest request,
        CancellationToken cancellationToken)
        where TRequest : IRequest<TResponse>
    {
        await using AsyncServiceScope scope = _scopeFactory!.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IRequestDispatcher>();

        return await dispatcher.DispatchAsync<TRequest, TResponse>(request, cancellationToken);
    }

    private CancellationTokenSource BeginOperation()
    {
        _operationCancellation?.Cancel();

        var operation = new CancellationTokenSource();
        _operationCancellation = operation;
        IsBusy = true;
        ErrorMessage = null;

        return operation;
    }

    private void EndOperation(CancellationTokenSource operation)
    {
        if (IsCurrentOperation(operation))
        {
            _operationCancellation = null;
            IsBusy = false;
        }

        operation.Dispose();
    }

    private bool IsCurrentOperation(CancellationTokenSource operation)
    {
        return ReferenceEquals(_operationCancellation, operation);
    }

    private void ApplyPreview(
        InstallPreviewResult preview,
        string? gameDirectory,
        IReadOnlyList<string> selectedOptionalGroups)
    {
        _preview = preview;
        _requestedGameDirectory = gameDirectory;

        var selected = selectedOptionalGroups.ToHashSet(StringComparer.OrdinalIgnoreCase);
        OptionalGroups.Clear();
        foreach ((string name, string? description) in preview.OptionalGroups)
        {
            OptionalGroups.Add(new InstallOptionalGroupViewModel(
                name,
                description,
                selected.Contains(name)));
        }

        OnPropertyChanged(nameof(IsDropZoneVisible));
        OnPropertyChanged(nameof(IsPreviewVisible));
        OnPropertyChanged(nameof(HasOptionalGroups));
        OnPropertyChanged(nameof(ModName));
        OnPropertyChanged(nameof(ModVersion));
        OnPropertyChanged(nameof(ModAuthor));
        OnPropertyChanged(nameof(ModDescription));
        OnPropertyChanged(nameof(HasModDescription));
        OnPropertyChanged(nameof(PackageFileName));
        OnPropertyChanged(nameof(TargetGameName));
        OnPropertyChanged(nameof(TargetGameDirectory));
    }

    private void SetPreview(InstallPreviewResult? preview)
    {
        _preview = preview;
        OnPropertyChanged(nameof(IsDropZoneVisible));
        OnPropertyChanged(nameof(IsPreviewVisible));
        OnPropertyChanged(nameof(ModName));
        OnPropertyChanged(nameof(ModVersion));
        OnPropertyChanged(nameof(ModAuthor));
        OnPropertyChanged(nameof(ModDescription));
        OnPropertyChanged(nameof(HasModDescription));
        OnPropertyChanged(nameof(PackageFileName));
        OnPropertyChanged(nameof(TargetGameName));
        OnPropertyChanged(nameof(TargetGameDirectory));
    }

    private IReadOnlyList<string> GetSelectedOptionalGroups()
    {
        return OptionalGroups
            .Where(group => group.IsSelected)
            .Select(group => group.Name)
            .ToArray();
    }

    private void SetError(ResourceKey key)
    {
        ErrorMessage = Localizer.Current.Get(key);
    }

    private void SetError(string message)
    {
        ErrorMessage = message;
    }

    private static string LocalizeError(OperationError error)
    {
        ResourceKey key = error.Code.Value switch
        {
            "mod_package.invalid_archive" => StringsKeys.InstallPage_ErrorInvalidArchive,
            "mod_package.missing_manifest" => StringsKeys.InstallPage_ErrorMissingManifest,
            "mod_package.multiple_manifests" => StringsKeys.InstallPage_ErrorMultipleManifests,
            "mod_package.unsafe_entry_path" => StringsKeys.InstallPage_ErrorUnsafeEntry,
            "mod_package.duplicate_entry" => StringsKeys.InstallPage_ErrorDuplicateEntry,
            "mod_package.package_too_large" => StringsKeys.InstallPage_ErrorPackageTooLarge,
            "mod_package.manifest_too_large" => StringsKeys.InstallPage_ErrorManifestTooLarge,
            "mod_package.missing_entry" => StringsKeys.InstallPage_ErrorMissingEntry,
            "file.not_found" => StringsKeys.InstallPage_ErrorFileNotFound,
            "file.access_denied" => StringsKeys.InstallPage_ErrorAccessDenied,
            "file.read_failed" or "file.system_failure" => StringsKeys.InstallPage_ErrorFileFailure,
            "game_directory.required" or "game_directory.not_found" => StringsKeys.InstallPage_ErrorGameDirectory,
            "patch.planning_failed" => StringsKeys.InstallPage_ErrorPatchPlanning,
            "operation.already_running" => StringsKeys.InstallPage_ErrorOperationRunning,
            "backup.recovery_required" => StringsKeys.InstallPage_ErrorRecoveryRequired,
            "install.preview_stale" or "install.file_integrity_mismatch" =>
                StringsKeys.InstallPage_ErrorPreviewStale,
            _ => StringsKeys.InstallPage_ErrorGeneric
        };

        return Localizer.Current.Get(key);
    }
}

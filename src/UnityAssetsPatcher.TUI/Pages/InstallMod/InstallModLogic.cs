using Microsoft.Extensions.DependencyInjection;
using UnityAssetsPatcher.Application;
using UnityAssetsPatcher.Application.Features.Install;
using UnityAssetsPatcher.Application.Installation;
using UnityAssetsPatcher.Application.Operations;
using UnityAssetsPatcher.Application.Patching;

namespace UnityAssetsPatcher.TUI.Pages.InstallMod;

public abstract record InstallModState
{
    public sealed record SelectPackage(OperationError? Error = null) : InstallModState;

    public sealed record Analyzing : InstallModState;

    public sealed record EnterGameDirectory(OperationError Error, string? Directory, bool IsPrompt) : InstallModState;

    public sealed record SelectOptionalGroups(
        IReadOnlyList<(string Name, string? Description)> Groups,
        IReadOnlyList<string> SelectedGroups,
        OperationError? Error = null) : InstallModState;

    public sealed record Preview(InstallPreviewResult Result, PatchDiagnostic? BlockingDiagnostic) : InstallModState;

    public sealed record Installing : InstallModState;

    public sealed record Installed(InstallModResult Result) : InstallModState;

    public sealed record InstallFailed(OperationError Error) : InstallModState;
}

public sealed class InstallModLogic : TerminalPageLogic<InstallModState>
{
    public bool VerboseLogging => _runtimeConfig.VerboseLogging;

    private readonly AppRuntimeConfig _runtimeConfig;
    private string? _modPath;
    private string? _gameDirectory;
    private string[] _selectedOptionalGroups = [];
    private IReadOnlyList<(string Name, string? Description)> _optionalGroups = [];
    private PreparedInstall? _preparedInstall;
    private bool _optionalGroupsConfirmed;
    private PreviewOrigin _previewOrigin;

    public InstallModLogic(IServiceScopeFactory scopeFactory, AppRuntimeConfig runtimeConfig)
        : base(scopeFactory, new InstallModState.SelectPackage())
    {
        ArgumentNullException.ThrowIfNull(runtimeConfig);
        _runtimeConfig = runtimeConfig;
    }

    public Task PreviewPackageAsync(string modPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modPath);

        return StartPreview(() =>
        {
            _modPath = modPath;
            _gameDirectory = null;
            _selectedOptionalGroups = [];
            _optionalGroups = [];
            _optionalGroupsConfirmed = false;
            _previewOrigin = PreviewOrigin.Package;
        });
    }

    public Task SubmitGameDirectoryAsync(string? gameDirectory)
    {
        return StartPreview(() =>
            {
                _gameDirectory = gameDirectory;
                _previewOrigin = PreviewOrigin.GameDirectory;
            },
            typeof(InstallModState.EnterGameDirectory));
    }

    public Task SubmitOptionalGroupsAsync(IReadOnlyList<string> selectedOptionalGroups)
    {
        ArgumentNullException.ThrowIfNull(selectedOptionalGroups);
        string[] selection = selectedOptionalGroups.ToArray();

        return StartPreview(() =>
        {
            _selectedOptionalGroups = selection;
            _optionalGroupsConfirmed = true;
            _previewOrigin = PreviewOrigin.OptionalGroups;
        }, typeof(InstallModState.SelectOptionalGroups));
    }

    public Task InstallAsync()
    {
        lock (SyncRoot)
        {
            if (IsUnavailable)
            {
                return Task.CompletedTask;
            }

            if (CurrentState is not InstallModState.Preview { BlockingDiagnostic: null })
            {
                throw new InvalidOperationException("The mod is not ready to install.");
            }

            var request = new InstallRequest(_modPath!, _gameDirectory)
            {
                SelectedOptionalGroups = _selectedOptionalGroups,
                PreparedInstall = _preparedInstall
            };

            CurrentState = new InstallModState.Installing();
            return StartOperation<InstallModRequest, InstallModResult>(
                new InstallModRequest(request),
                CompleteInstall);
        }
    }

    private Task StartPreview(Action configure, Type? requiredState = null)
    {
        lock (SyncRoot)
        {
            if (IsUnavailable)
            {
                return Task.CompletedTask;
            }

            if (requiredState is not null && !requiredState.IsInstanceOfType(CurrentState))
            {
                throw new InvalidOperationException(
                    $"The current install state is '{CurrentState.GetType().Name}', not '{requiredState.Name}'.");
            }

            configure();
            _preparedInstall = null;

            var request = new InstallRequest(_modPath!, _gameDirectory)
            {
                SelectedOptionalGroups = _selectedOptionalGroups,
                IncludePatchPreviewDetails = false
            };

            CurrentState = new InstallModState.Analyzing();
            return StartOperation<PreviewInstallRequest, InstallPreviewResult>(
                new PreviewInstallRequest(request),
                CompletePreview);
        }
    }

    private void CompletePreview(OperationResult<InstallPreviewResult> result)
    {
        if (result is OperationFailed<InstallPreviewResult> failed)
        {
            bool needsGameDirectory =
                (failed.Error.Code == GameDirectoryErrorCodes.Required ||
                 failed.Error.Code == GameDirectoryErrorCodes.NotFound) &&
                string.IsNullOrEmpty(_gameDirectory);

            CurrentState = needsGameDirectory
                ? new InstallModState.EnterGameDirectory(
                    failed.Error,
                    _gameDirectory,
                    true)
                : CreateRetryState(failed.Error);
            return;
        }

        InstallPreviewResult preview = ((OperationSucceeded<InstallPreviewResult>)result).Value;
        _preparedInstall = preview.PreparedInstall;

        if (preview.OptionalGroups.Count > 0 && !_optionalGroupsConfirmed)
        {
            _optionalGroups = preview.OptionalGroups.ToArray();
            CurrentState = new InstallModState.SelectOptionalGroups(
                _optionalGroups,
                _selectedOptionalGroups);
            return;
        }

        PatchDiagnostic? diagnostic = preview.Changes
            .Select(change => change.Preview?.Diagnostic)
            .FirstOrDefault(candidate => candidate is not null);
        CurrentState = new InstallModState.Preview(preview, diagnostic);
    }

    private InstallModState CreateRetryState(OperationError error)
    {
        return _previewOrigin switch
        {
            PreviewOrigin.GameDirectory => new InstallModState.EnterGameDirectory(
                error,
                _gameDirectory,
                false),
            PreviewOrigin.OptionalGroups => new InstallModState.SelectOptionalGroups(
                _optionalGroups,
                _selectedOptionalGroups,
                error),
            _ => new InstallModState.SelectPackage(error)
        };
    }

    private void CompleteInstall(OperationResult<InstallModResult> result)
    {
        CurrentState = result is OperationSucceeded<InstallModResult> succeeded
            ? new InstallModState.Installed(succeeded.Value)
            : new InstallModState.InstallFailed(((OperationFailed<InstallModResult>)result).Error);
    }

    private enum PreviewOrigin
    {
        Package,
        GameDirectory,
        OptionalGroups
    }
}

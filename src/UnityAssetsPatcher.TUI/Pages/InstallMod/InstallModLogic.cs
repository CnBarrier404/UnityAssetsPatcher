using Microsoft.Extensions.DependencyInjection;
using UnityAssetsPatcher.Application;
using UnityAssetsPatcher.Application.Features.Install;
using UnityAssetsPatcher.Application.Installation;
using UnityAssetsPatcher.Application.Messaging;
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

public sealed class InstallModLogic : IDisposable
{
    public InstallModState State
    {
        get
        {
            lock (_sync)
            {
                return _state;
            }
        }
    }

    public bool IsWorking
    {
        get
        {
            lock (_sync)
            {
                return _isWorking;
            }
        }
    }

    public bool VerboseLogging => _runtimeConfig.VerboseLogging;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AppRuntimeConfig _runtimeConfig;
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private readonly Lock _sync = new();
    private InstallModState _state = new InstallModState.SelectPackage();
    private string? _modPath;
    private string? _gameDirectory;
    private string[] _selectedOptionalGroups = [];
    private IReadOnlyList<(string Name, string? Description)> _optionalGroups = [];
    private PreparedInstall? _preparedInstall;
    private bool _optionalGroupsConfirmed;
    private PreviewOrigin _previewOrigin;
    private bool _isWorking;
    private bool _isDisposed;
    private bool _isCancellationPending;

    public InstallModLogic(IServiceScopeFactory scopeFactory, AppRuntimeConfig runtimeConfig)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(runtimeConfig);
        _scopeFactory = scopeFactory;
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
        lock (_sync)
        {
            if (_isWorking || _isDisposed)
            {
                return Task.CompletedTask;
            }

            if (_state is not InstallModState.Preview { BlockingDiagnostic: null })
            {
                throw new InvalidOperationException("The mod is not ready to install.");
            }

            var request = new InstallRequest(_modPath!, _gameDirectory)
            {
                SelectedOptionalGroups = _selectedOptionalGroups,
                PreparedInstall = _preparedInstall
            };

            return StartOperation<InstallModRequest, InstallModResult>(
                new InstallModRequest(request),
                new InstallModState.Installing(),
                CompleteInstall);
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _preparedInstall = null;
            _isCancellationPending = true;
        }

        try
        {
            _lifetimeCancellation.Cancel();
        }
        finally
        {
            lock (_sync)
            {
                _isCancellationPending = false;
                if (!_isWorking)
                {
                    _lifetimeCancellation.Dispose();
                }
            }
        }
    }

    private Task StartPreview(Action configure, Type? requiredState = null)
    {
        lock (_sync)
        {
            if (_isWorking || _isDisposed)
            {
                return Task.CompletedTask;
            }

            if (requiredState is not null && !requiredState.IsInstanceOfType(_state))
            {
                throw new InvalidOperationException(
                    $"The current install state is '{_state.GetType().Name}', not '{requiredState.Name}'.");
            }

            configure();
            _preparedInstall = null;

            var request = new InstallRequest(_modPath!, _gameDirectory)
            {
                SelectedOptionalGroups = _selectedOptionalGroups,
                IncludePatchPreviewDetails = false
            };

            return StartOperation<PreviewInstallRequest, InstallPreviewResult>(
                new PreviewInstallRequest(request),
                new InstallModState.Analyzing(),
                CompletePreview);
        }
    }

    private Task StartOperation<TRequest, TResult>(
        TRequest request,
        InstallModState workingState,
        Action<OperationResult<TResult>> complete)
        where TRequest : IRequest<OperationResult<TResult>>
    {
        _isWorking = true;
        _state = workingState;
        CancellationToken cancellationToken = _lifetimeCancellation.Token;

        return RunOperationAsync(request, complete, cancellationToken);
    }

    private async Task RunOperationAsync<TRequest, TResult>(
        TRequest request,
        Action<OperationResult<TResult>> complete,
        CancellationToken cancellationToken)
        where TRequest : IRequest<OperationResult<TResult>>
    {
        try
        {
            var result = await DispatchOnWorkerAsync<TRequest, TResult>(
                request,
                cancellationToken).ConfigureAwait(false);

            lock (_sync)
            {
                if (!_isDisposed)
                {
                    complete(result);
                }
            }
        }
        catch (OperationCanceledException exception)
            when (cancellationToken.IsCancellationRequested &&
                  exception.CancellationToken == cancellationToken) { }
        finally
        {
            EndOperation();
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

            _state = needsGameDirectory
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
            _state = new InstallModState.SelectOptionalGroups(
                _optionalGroups,
                _selectedOptionalGroups);
            return;
        }

        PatchDiagnostic? diagnostic = preview.Changes
            .Select(change => change.Preview?.Diagnostic)
            .FirstOrDefault(candidate => candidate is not null);
        _state = new InstallModState.Preview(preview, diagnostic);
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
        _state = result is OperationSucceeded<InstallModResult> succeeded
            ? new InstallModState.Installed(succeeded.Value)
            : new InstallModState.InstallFailed(((OperationFailed<InstallModResult>)result).Error);
    }

    private void EndOperation()
    {
        lock (_sync)
        {
            _isWorking = false;
            if (_isDisposed && !_isCancellationPending)
            {
                _lifetimeCancellation.Dispose();
            }
        }
    }

    private async Task<OperationResult<TResult>> DispatchOnWorkerAsync<TRequest, TResult>(
        TRequest request,
        CancellationToken cancellationToken)
        where TRequest : IRequest<OperationResult<TResult>>
    {
        return await Task.Run(async () =>
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            var dispatcher = scope.ServiceProvider.GetRequiredService<IRequestDispatcher>();
            return await dispatcher.DispatchAsync<TRequest, OperationResult<TResult>>(
                request,
                cancellationToken).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
    }

    private enum PreviewOrigin
    {
        Package,
        GameDirectory,
        OptionalGroups
    }
}

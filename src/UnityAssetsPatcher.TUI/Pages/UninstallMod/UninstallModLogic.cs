using Microsoft.Extensions.DependencyInjection;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Features.Uninstall;
using UnityAssetsPatcher.Application.Installation;
using UnityAssetsPatcher.Application.Operations;

namespace UnityAssetsPatcher.TUI.Pages.UninstallMod;

public abstract record UninstallModState
{
    public sealed record LoadingInstalledMods : UninstallModState;

    public sealed record InstalledMods(IReadOnlyList<InstallRecordSummary> Records) : UninstallModState;

    public sealed record Analyzing : UninstallModState;

    public sealed record EnterGameDirectory(string InstallId, OperationError Error) : UninstallModState;

    public sealed record Preview(UninstallPreviewResult Result) : UninstallModState;

    public sealed record Uninstalling : UninstallModState;

    public sealed record Uninstalled(UninstallModResult Result) : UninstallModState;

    public sealed record Failed(OperationError Error) : UninstallModState;
}

public sealed class UninstallModLogic : TerminalPageLogic<UninstallModState>
{
    public UninstallModLogic(IServiceScopeFactory scopeFactory)
        : base(scopeFactory, new UninstallModState.LoadingInstalledMods()) { }

    public Task LoadInstalledModsAsync()
    {
        lock (SyncRoot)
        {
            if (IsUnavailable)
            {
                return Task.CompletedTask;
            }

            CurrentState = new UninstallModState.LoadingInstalledMods();
            return StartOperation<ListInstalledModsRequest, IReadOnlyList<InstallRecordSummary>>(
                new ListInstalledModsRequest(),
                CompleteInstalledMods);
        }
    }

    public Task PreviewAsync(string installId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(installId);

        lock (SyncRoot)
        {
            if (IsUnavailable)
            {
                return Task.CompletedTask;
            }

            if (CurrentState is not UninstallModState.InstalledMods)
            {
                throw new InvalidOperationException("An installed mod is not ready for preview.");
            }

            return StartPreview(installId, null);
        }
    }

    public Task SubmitGameDirectoryAsync(string gameDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameDirectory);

        lock (SyncRoot)
        {
            if (IsUnavailable)
            {
                return Task.CompletedTask;
            }

            if (CurrentState is not UninstallModState.EnterGameDirectory state)
            {
                throw new InvalidOperationException("A game directory is not required.");
            }

            return StartPreview(state.InstallId, gameDirectory);
        }
    }

    public Task UninstallAsync()
    {
        lock (SyncRoot)
        {
            if (IsUnavailable)
            {
                return Task.CompletedTask;
            }

            if (CurrentState is not UninstallModState.Preview { Result.CanUninstall: true } state)
            {
                throw new InvalidOperationException("The mod is not ready to uninstall.");
            }

            var request = new UninstallModRequest(
                state.Result.InstallId,
                state.Result.GameDirectory);

            CurrentState = new UninstallModState.Uninstalling();
            return StartOperation<UninstallModRequest, UninstallModResult>(request, CompleteUninstall);
        }
    }

    private Task StartPreview(string installId, string? gameDirectory)
    {
        CurrentState = new UninstallModState.Analyzing();
        return StartOperation<UninstallPreviewRequest, UninstallPreviewResult>(
            new UninstallPreviewRequest(installId, gameDirectory),
            result => CompletePreview(result, installId, gameDirectory));
    }

    private void CompleteInstalledMods(
        OperationResult<IReadOnlyList<InstallRecordSummary>> result)
    {
        CurrentState = result is OperationSucceeded<IReadOnlyList<InstallRecordSummary>> succeeded
            ? new UninstallModState.InstalledMods(succeeded.Value)
            : new UninstallModState.Failed(
                ((OperationFailed<IReadOnlyList<InstallRecordSummary>>)result).Error);
    }

    private void CompletePreview(
        OperationResult<UninstallPreviewResult> result,
        string installId,
        string? gameDirectory)
    {
        if (result is OperationSucceeded<UninstallPreviewResult> succeeded)
        {
            CurrentState = new UninstallModState.Preview(succeeded.Value);
            return;
        }

        OperationError error = ((OperationFailed<UninstallPreviewResult>)result).Error;
        bool needsGameDirectory =
            (error.Code == GameDirectoryErrorCodes.Required ||
             error.Code == GameDirectoryErrorCodes.NotFound) &&
            gameDirectory is null;

        CurrentState = needsGameDirectory
            ? new UninstallModState.EnterGameDirectory(installId, error)
            : new UninstallModState.Failed(error);
    }

    private void CompleteUninstall(OperationResult<UninstallModResult> result)
    {
        CurrentState = result is OperationSucceeded<UninstallModResult> succeeded
            ? new UninstallModState.Uninstalled(succeeded.Value)
            : new UninstallModState.Failed(((OperationFailed<UninstallModResult>)result).Error);
    }
}

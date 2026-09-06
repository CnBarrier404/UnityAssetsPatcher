using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Composition;
using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Application.Installation;
using UnityAssetsPatcher.Application.Messaging;
using UnityAssetsPatcher.Application.Operations;
using UnityAssetsPatcher.Application.Mods;
using UnityAssetsPatcher.Application.Patching;
using UnityAssetsPatcher.Application.Repository;
using UnityAssetsPatcher.Application.Uninstallation;

namespace UnityAssetsPatcher.Application.Features.Uninstall;

public sealed class UninstallModHandler :
    IRequestHandler<UninstallPreviewRequest, OperationResult<UninstallPreviewResult>>,
    IRequestHandler<UninstallModRequest, OperationResult<UninstallModResult>>,
    IRequestHandler<ListInstalledModsRequest, OperationResult<IReadOnlyList<InstallRecordSummary>>>
{
    private readonly UninstallPlanner _planner;
    private readonly IRepository _repository;
    private readonly ILogger<UninstallModHandler> _logger;

    public UninstallModHandler(
        UninstallPlanner planner,
        IRepository repository,
        ILogger<UninstallModHandler>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(planner);
        ArgumentNullException.ThrowIfNull(repository);

        _planner = planner;
        _repository = repository;
        _logger = logger ?? NullLogger<UninstallModHandler>.Instance;
    }

    public async Task<OperationResult<UninstallPreviewResult>> HandleAsync(
        UninstallPreviewRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var result = await InvokeAsync(
            () => PreviewAsync(request, cancellationToken),
            DirectoryError(request.GameDirectory),
            nameof(PreviewAsync)).ConfigureAwait(false);

        return result;
    }

    public async Task<OperationResult<IReadOnlyList<InstallRecordSummary>>> HandleAsync(
        ListInstalledModsRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var result = await InvokeAsync(
            () => Task.FromResult(ListInstalledMods()),
            null,
            nameof(ListInstalledMods)).ConfigureAwait(false);

        return result;
    }

    public async Task<OperationResult<UninstallModResult>> HandleAsync(
        UninstallModRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var result = await InvokeAsync(
            () => UninstallAsync(request, cancellationToken),
            DirectoryError(request.GameDirectory),
            nameof(UninstallAsync)).ConfigureAwait(false);

        return result;
    }

    private async Task<UninstallPreviewResult> PreviewAsync(
        UninstallPreviewRequest request,
        CancellationToken cancellationToken)
    {
        return await _planner.BuildPreviewAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private async Task<UninstallModResult> UninstallAsync(
        UninstallModRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Uninstalling mod install {InstallId}", request.InstallId);
        UninstallPlan plan = await _planner.BuildUninstallAsync(request, cancellationToken).ConfigureAwait(false);
        UninstallModResult result = await _repository.UninstallModAsync(plan, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Uninstalled {ModName} {ModVersion}: {ChangedFileCount} files composed",
            result.ModName,
            result.ModVersion,
            result.ChangedFiles.Count);

        return result;
    }

    private IReadOnlyList<InstallRecordSummary> ListInstalledMods()
    {
        _logger.LogInformation("Listing installed mods");

        return _repository.ListInstalledMods();
    }

    private async Task<OperationResult<TResult>> InvokeAsync<TResult>(
        Func<Task<TResult>> operation,
        OperationErrorCode? directoryError,
        string operationName)
    {
        try
        {
            TResult result = await operation().ConfigureAwait(false);

            return new OperationSucceeded<TResult>(result);
        }
        catch (RepositoryRecoveryException exception)
        {
            var error = new OperationError(
                RepositoryErrorCodes.RecoveryRequired,
                recovery: exception.Recovery);
            _logger.LogWarning(
                "Uninstall operation {OperationName} requires backup recovery",
                operationName);

            return new OperationFailed<TResult>(error);
        }
        catch (PatchPlanningException exception)
        {
            var error = new OperationError(
                PatchErrorCodes.PlanningFailed,
                new Dictionary<string, object?>
                {
                    ["diagnosticCode"] = exception.Diagnostic.Code.ToString(),
                    ["path"] = exception.Diagnostic.AssetsFilePath
                });

            return new OperationFailed<TResult>(error);
        }
        catch (LayerPackageValidationException exception)
        {
            return new OperationFailed<TResult>(exception.Error);
        }
        catch (LayerPackageIntegrityException exception)
        {
            return ExpectedFailure<TResult>(
                operationName,
                ModOperationErrorCodes.FileIntegrityMismatch,
                null,
                exception.PackagePath);
        }
        catch (FileNotFoundException exception)
        {
            return ExpectedFailure<TResult>(
                operationName, FileErrorCodes.NotFound, exception.Message, exception.FileName);
        }
        catch (DirectoryNotFoundException exception)
        {
            OperationErrorCode code = directoryError ?? FileErrorCodes.DirectoryNotFound;

            return ExpectedFailure<TResult>(operationName, code, exception.Message);
        }
        catch (UnauthorizedAccessException exception)
        {
            return ExpectedFailure<TResult>(operationName, FileErrorCodes.AccessDenied, exception.Message);
        }
        catch (IOException exception)
        {
            return ExpectedFailure<TResult>(operationName, FileErrorCodes.SystemFailure, exception.Message);
        }
        catch (KeyNotFoundException exception)
        {
            return ExpectedFailure<TResult>(
                operationName, ModOperationErrorCodes.InstallRecordNotFound, exception.Message);
        }
        catch (RepositoryOperationLockedException exception)
        {
            return ExpectedFailure<TResult>(operationName, RepositoryErrorCodes.OperationAlreadyRunning,
                exception.Message);
        }
        catch (UnsupportedRepositoryFormatException exception)
        {
            return ExpectedFailure<TResult>(
                operationName,
                RepositoryErrorCodes.UnsupportedVersion,
                exception.Message);
        }
        catch (Exception exception) when (exception is UninstallValidationException or UninstallCompositionException)
        {
            OperationErrorCode code = operationName == nameof(UninstallAsync)
                ? ModOperationErrorCodes.FileIntegrityMismatch
                : RepositoryErrorCodes.Unsafe;

            return ExpectedFailure<TResult>(operationName, code, exception.Message);
        }
    }

    private OperationFailed<TResult> ExpectedFailure<TResult>(
        string operationName,
        OperationErrorCode code,
        string? detail,
        string? path = null)
    {
        var parameters = new Dictionary<string, object?>();
        if (!string.IsNullOrWhiteSpace(detail))
        {
            parameters["detail"] = detail;
        }

        if (!string.IsNullOrWhiteSpace(path))
        {
            parameters["path"] = path;
        }

        var error = new OperationError(code, parameters);
        _logger.LogWarning(
            "Uninstall operation {OperationName} failed with {ErrorCode}: {@Parameters}",
            operationName,
            code,
            parameters);

        return new OperationFailed<TResult>(error);
    }

    private static OperationErrorCode DirectoryError(string? gameDirectory)
    {
        return string.IsNullOrWhiteSpace(gameDirectory)
            ? GameDirectoryErrorCodes.Required
            : GameDirectoryErrorCodes.NotFound;
    }
}

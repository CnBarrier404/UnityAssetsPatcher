using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Application.Messaging;
using UnityAssetsPatcher.Application.Operations;
using UnityAssetsPatcher.Application.Packages;
using UnityAssetsPatcher.Application.Patching;
using UnityAssetsPatcher.Application.Repository;
using UnityAssetsPatcher.Application.Uninstallation;
using UnityAssetsPatcher.Application.Workflows;

namespace UnityAssetsPatcher.Application.Features.Uninstall;

public sealed class UninstallModHandler :
    IRequestHandler<UninstallPreviewRequest, OperationResult<UninstallPreviewResult>>,
    IRequestHandler<UninstallModRequest, OperationResult<UninstallModResult>>
{
    private readonly UninstallPlanner _planner;
    private readonly IRepositoryService _repository;
    private readonly ILogger<UninstallModHandler> _logger;

    public UninstallModHandler(
        UninstallPlanner planner,
        IRepositoryService repository,
        ILogger<UninstallModHandler>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(planner);
        ArgumentNullException.ThrowIfNull(repository);

        _planner = planner;
        _repository = repository;
        _logger = logger ?? NullLogger<UninstallModHandler>.Instance;
    }

    public Task<OperationResult<UninstallPreviewResult>> HandleAsync(
        UninstallPreviewRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        OperationResult<UninstallPreviewResult> result = Invoke(
            () => Preview(request),
            DirectoryError(request.GameDirectory),
            nameof(Preview));

        return Task.FromResult(result);
    }

    public Task<OperationResult<UninstallModResult>> HandleAsync(
        UninstallModRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        OperationResult<UninstallModResult> result = Invoke(
            () => Uninstall(request),
            DirectoryError(request.GameDirectory),
            nameof(Uninstall));

        return Task.FromResult(result);
    }

    private UninstallPreviewResult Preview(UninstallPreviewRequest request)
    {
        return _planner.BuildPreview(request);
    }

    private UninstallModResult Uninstall(UninstallModRequest request)
    {
        _logger.LogInformation("Uninstalling mod install {InstallId}", request.InstallId);
        UninstallPlan plan = _planner.BuildUninstall(request);
        UninstallModResult result = _repository.UninstallMod(plan);

        _logger.LogInformation(
            "Uninstalled {ModName} {ModVersion}: {ChangedFileCount} files composed",
            result.ModName,
            result.ModVersion,
            result.ChangedFiles.Count);

        return result;
    }

    private OperationResult<TResult> Invoke<TResult>(
        Func<TResult> operation,
        OperationErrorCode directoryError,
        string operationName)
    {
        try
        {
            TResult result = operation();

            return new OperationSucceeded<TResult>(result);
        }
        catch (RepositoryRecoveryException exception)
        {
            var error = new OperationError(
                WorkflowErrorCodes.RecoveryRequired,
                recovery: exception.Recovery);
            _logger.LogWarning(
                "Uninstall operation {OperationName} requires backup recovery",
                operationName);

            return new OperationFailed<TResult>(error);
        }
        catch (PatchPlanningException exception)
        {
            var error = new OperationError(
                WorkflowErrorCodes.PatchPlanningFailed,
                new Dictionary<string, object?>
                {
                    ["diagnosticCode"] = exception.Diagnostic.Code.ToString(),
                    ["path"] = exception.Diagnostic.AssetsFilePath,
                });

            return new OperationFailed<TResult>(error);
        }
        catch (FileNotFoundException exception)
        {
            return ExpectedFailure<TResult>(
                operationName, FileErrorCodes.NotFound, exception.Message, exception.FileName);
        }
        catch (DirectoryNotFoundException exception)
        {
            return ExpectedFailure<TResult>(operationName, directoryError, exception.Message);
        }
        catch (UnauthorizedAccessException exception)
        {
            return ExpectedFailure<TResult>(operationName, FileErrorCodes.AccessDenied, exception.Message);
        }
        catch (IOException exception)
        {
            return ExpectedFailure<TResult>(operationName, FileErrorCodes.SystemFailure, exception.Message);
        }
        catch (JsonException exception)
        {
            return ExpectedFailure<TResult>(operationName, ModPackageErrorCodes.InvalidPackage, exception.Message);
        }
        catch (InvalidDataException exception)
        {
            return ExpectedFailure<TResult>(operationName, ModPackageErrorCodes.InvalidPackage, exception.Message);
        }
        catch (KeyNotFoundException exception)
        {
            return ExpectedFailure<TResult>(
                operationName, WorkflowErrorCodes.InstallRecordNotFound, exception.Message);
        }
        catch (LegacyRepositoryWriteException exception)
        {
            return ExpectedFailure<TResult>(
                operationName,
                WorkflowErrorCodes.UnsupportedRepositoryVersion,
                exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            OperationErrorCode code = operationName == nameof(Uninstall)
                ? WorkflowErrorCodes.FileIntegrityMismatch
                : WorkflowErrorCodes.RepositoryUnsafe;

            return ExpectedFailure<TResult>(operationName, code, exception.Message);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Uninstall operation {OperationName} failed", operationName);

            throw;
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
            ? WorkflowErrorCodes.GameDirectoryRequired
            : WorkflowErrorCodes.GameDirectoryNotFound;
    }
}

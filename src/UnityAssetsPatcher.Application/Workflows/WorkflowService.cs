using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using UnityAssetsPatcher.Application.Backups;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Application.Installation;
using UnityAssetsPatcher.Application.Operations;
using UnityAssetsPatcher.Application.Packages;
using UnityAssetsPatcher.Application.Patching;
using UnityAssetsPatcher.Domain.Assets;

namespace UnityAssetsPatcher.Application.Workflows;

public sealed class WorkflowService : IWorkflowService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WorkflowService> _logger;

    public WorkflowService(IServiceScopeFactory scopeFactory, ILogger<WorkflowService>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        _scopeFactory = scopeFactory;
        _logger = logger ?? NullLogger<WorkflowService>.Instance;
    }

    public OperationResult<BackupRecoveryPreview> PreviewPendingTransaction(string gameDirectory)
    {
        return Invoke<BackupRepository, BackupRecoveryPreview>(repository =>
            repository.PreviewPendingTransaction(gameDirectory));
    }

    public OperationResult<BackupRecoveryReport> RecoverPendingTransactions(string gameDirectory)
    {
        return Invoke<BackupRepository, BackupRecoveryReport>(repository =>
            repository.RecoverPendingTransactions(gameDirectory));
    }

    public OperationResult<BackupRecoveryReport> CheckPendingTransactions()
    {
        return Invoke<BackupRepository, BackupRecoveryReport>(repository => repository.CheckPendingTransactions());
    }

    public OperationResult<InspectListResult> InspectList(InspectListRequest request)
    {
        return Invoke<InspectAssetsWorkflow, InspectListResult>(workflow => workflow.List(request));
    }

    public OperationResult<AssetField> InspectFields(InspectFieldsRequest request)
    {
        return Invoke<InspectAssetsWorkflow, AssetField>(workflow => workflow.Fields(request));
    }

    public OperationResult<InstallPreviewResult> PreviewInstall(InstallRequest request)
    {
        return Invoke<InstallModWorkflow, InstallPreviewResult>(
            workflow => workflow.Preview(request),
            DirectoryError(request.GameDirectory));
    }

    public OperationResult<InstallModResult> Install(InstallRequest request)
    {
        return Invoke<InstallModWorkflow, InstallModResult>(
            workflow => workflow.Install(request),
            DirectoryError(request.GameDirectory));
    }

    public OperationResult<IReadOnlyList<InstallRecordSummary>> ListInstalledMods()
    {
        return Invoke<UninstallModWorkflow, IReadOnlyList<InstallRecordSummary>>(workflow => workflow.ListInstalled());
    }

    public OperationResult<UninstallPreviewResult> PreviewUninstall(UninstallPreviewRequest request)
    {
        return Invoke<UninstallModWorkflow, UninstallPreviewResult>(
            workflow => workflow.Preview(request),
            DirectoryError(request.GameDirectory));
    }

    public OperationResult<UninstallModResult> Uninstall(UninstallModRequest request)
    {
        return Invoke<UninstallModWorkflow, UninstallModResult>(
            workflow => workflow.Uninstall(request),
            DirectoryError(request.GameDirectory));
    }

    private OperationResult<TResult> Invoke<TService, TResult>(
        Func<TService, TResult> operation,
        OperationErrorCode? directoryError = null,
        [CallerMemberName] string operationName = "")
        where TService : notnull
    {
        using IServiceScope scope = _scopeFactory.CreateScope();

        try
        {
            TResult result = operation(scope.ServiceProvider.GetRequiredService<TService>());

            return new OperationSucceeded<TResult>(result);
        }
        catch (BackupRecoveryException exception)
        {
            var error = new OperationError(
                WorkflowErrorCodes.RecoveryRequired,
                recovery: exception.Recovery);
            _logger.LogWarning(
                "Workflow operation {OperationName} requires backup recovery",
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
        catch (InstallPreparationStaleException)
        {
            return ExpectedFailure<TResult>(operationName, WorkflowErrorCodes.InstallPreviewStale, null);
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
        catch (JsonException exception)
        {
            return ExpectedFailure<TResult>(operationName, ContentError(), exception.Message);
        }
        catch (InvalidDataException exception)
        {
            return ExpectedFailure<TResult>(operationName, ContentError(), exception.Message);
        }
        catch (KeyNotFoundException exception) when (operationName is nameof(PreviewUninstall) or nameof(Uninstall))
        {
            return ExpectedFailure<TResult>(operationName, WorkflowErrorCodes.InstallRecordNotFound,
                exception.Message);
        }
        catch (NotSupportedException exception) when (IsUserContentOperation(operationName))
        {
            OperationErrorCode code = operationName switch
            {
                nameof(PreviewInstall) or nameof(Install) => ModPackageErrorCodes.InvalidPackage,
                _ => WorkflowErrorCodes.UnsupportedBackupRepositoryVersion,
            };

            return ExpectedFailure<TResult>(operationName, code, exception.Message);
        }
        catch (InvalidOperationException exception) when (TryMapInvalidOperation(
                                                              operationName, exception, out OperationErrorCode code))
        {
            return ExpectedFailure<TResult>(operationName, code, exception.Message);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Workflow operation {OperationName} failed", operationName);

            throw;
        }
    }

    private OperationResult<TResult> ExpectedFailure<TResult>(
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
            "Workflow operation {OperationName} failed with {ErrorCode}: {@Parameters}",
            operationName,
            code,
            parameters);

        return new OperationFailed<TResult>(error);
    }

    private static OperationErrorCode ContentError()
    {
        return ModPackageErrorCodes.InvalidPackage;
    }

    private static OperationErrorCode DirectoryError(string? gameDirectory)
    {
        return string.IsNullOrWhiteSpace(gameDirectory)
            ? WorkflowErrorCodes.GameDirectoryRequired
            : WorkflowErrorCodes.GameDirectoryNotFound;
    }

    private static bool IsUserContentOperation(string operationName)
    {
        return operationName is nameof(PreviewInstall) or
            nameof(Install) or
            nameof(CheckPendingTransactions) or
            nameof(PreviewPendingTransaction) or
            nameof(RecoverPendingTransactions) or
            nameof(ListInstalledMods);
    }

    private static bool TryMapInvalidOperation(
        string operationName,
        InvalidOperationException exception,
        out OperationErrorCode code)
    {
        code = operationName switch
        {
            nameof(PreviewInstall) or nameof(Install) => ModPackageErrorCodes.InvalidPackage,
            nameof(InspectFields) => WorkflowErrorCodes.AssetNotFound,
            nameof(ListInstalledMods) or
                nameof(CheckPendingTransactions) or
                nameof(PreviewPendingTransaction) or
                nameof(RecoverPendingTransactions) => exception.InnerException is IOException
                    ? WorkflowErrorCodes.OperationAlreadyRunning
                    : WorkflowErrorCodes.BackupRepositoryUnsafe,
            nameof(PreviewUninstall) => WorkflowErrorCodes.BackupRepositoryUnsafe,
            nameof(Uninstall) => WorkflowErrorCodes.FileIntegrityMismatch,
            _ => throw new ArgumentOutOfRangeException(nameof(operationName), operationName, null),
        };

        return operationName is nameof(PreviewInstall) or
            nameof(Install) or
            nameof(InspectFields) or
            nameof(ListInstalledMods) or
            nameof(CheckPendingTransactions) or
            nameof(PreviewPendingTransaction) or
            nameof(RecoverPendingTransactions) or
            nameof(PreviewUninstall) or
            nameof(Uninstall);
    }
}

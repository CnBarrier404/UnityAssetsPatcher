using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using UnityAssetsPatcher.Application.Repository;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Application.Operations;
using UnityAssetsPatcher.Application.Packages;
using UnityAssetsPatcher.Application.Patching;
using UnityAssetsPatcher.Application.Uninstallation;

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

    public OperationResult<RepositoryRecoveryPreview> PreviewPendingTransaction(string gameDirectory)
    {
        return Invoke<RepositoryService, RepositoryRecoveryPreview>(repository =>
            repository.PreviewPendingTransaction(gameDirectory));
    }

    public OperationResult<RepositoryRecoveryReport> RecoverPendingTransactions(string gameDirectory)
    {
        return Invoke<RepositoryService, RepositoryRecoveryReport>(repository =>
            repository.RecoverPendingTransactions(gameDirectory));
    }

    public OperationResult<RepositoryRecoveryReport> CheckPendingTransactions()
    {
        return Invoke<RepositoryService, RepositoryRecoveryReport>(repository => repository.CheckPendingTransactions());
    }

    public OperationResult<IReadOnlyList<InstallRecordSummary>> ListInstalledMods()
    {
        return Invoke<IRepository, IReadOnlyList<InstallRecordSummary>>(repository =>
            repository.ListInstalledMods());
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
        catch (RepositoryRecoveryException exception)
        {
            var error = new OperationError(
                RepositoryErrorCodes.RecoveryRequired,
                recovery: exception.Recovery);
            _logger.LogWarning(
                "Workflow operation {OperationName} requires backup recovery",
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
        catch (LegacyRepositoryWriteException exception)
        {
            return ExpectedFailure<TResult>(
                operationName,
                RepositoryErrorCodes.UnsupportedVersion,
                exception.Message);
        }
        catch (NotSupportedException exception) when (IsUserContentOperation(operationName))
        {
            OperationErrorCode code = operationName switch
            {
                _ => RepositoryErrorCodes.UnsupportedVersion,
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

    private static bool IsUserContentOperation(string operationName)
    {
        return operationName is nameof(CheckPendingTransactions) or
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
            nameof(ListInstalledMods) or
                nameof(CheckPendingTransactions) or
                nameof(PreviewPendingTransaction) or
                nameof(RecoverPendingTransactions) => exception.InnerException is IOException
                    ? RepositoryErrorCodes.OperationAlreadyRunning
                    : RepositoryErrorCodes.Unsafe,
            _ => throw new ArgumentOutOfRangeException(nameof(operationName), operationName, null),
        };

        return operationName is nameof(ListInstalledMods) or
            nameof(CheckPendingTransactions) or
            nameof(PreviewPendingTransaction) or
            nameof(RecoverPendingTransactions);
    }
}

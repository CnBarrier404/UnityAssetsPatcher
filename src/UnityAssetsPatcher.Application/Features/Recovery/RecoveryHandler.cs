using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Application.Messaging;
using UnityAssetsPatcher.Application.Operations;
using UnityAssetsPatcher.Application.Mods;
using UnityAssetsPatcher.Application.Patching;
using UnityAssetsPatcher.Application.Repository;

namespace UnityAssetsPatcher.Application.Features.Recovery;

public sealed class RecoveryHandler :
    IRequestHandler<PreviewRecoveryRequest, OperationResult<RepositoryRecoveryPreview>>,
    IRequestHandler<RecoverRecoveryRequest, OperationResult<RepositoryRecoveryReport>>
{
    private readonly RepositoryService _repository;
    private readonly ILogger<RecoveryHandler> _logger;

    public RecoveryHandler(
        RepositoryService repository,
        ILogger<RecoveryHandler>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(repository);

        _repository = repository;
        _logger = logger ?? NullLogger<RecoveryHandler>.Instance;
    }

    public Task<OperationResult<RepositoryRecoveryPreview>> HandleAsync(
        PreviewRecoveryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var result = Invoke(
            () => _repository.PreviewPendingTransaction(request.GameDirectory),
            nameof(PreviewRecoveryRequest));

        return Task.FromResult(result);
    }

    public Task<OperationResult<RepositoryRecoveryReport>> HandleAsync(
        RecoverRecoveryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var result = Invoke(
            () => _repository.RecoverPendingTransactions(request.GameDirectory),
            nameof(RecoverRecoveryRequest));

        return Task.FromResult(result);
    }

    private OperationResult<TResult> Invoke<TResult>(Func<TResult> operation, string operationName)
    {
        try
        {
            TResult result = operation();

            return new OperationSucceeded<TResult>(result);
        }
        catch (RepositoryRecoveryException exception)
        {
            var error = new OperationError(
                RepositoryErrorCodes.RecoveryRequired,
                recovery: exception.Recovery);
            _logger.LogWarning(
                "Recovery operation {OperationName} requires backup recovery",
                operationName);

            return new OperationFailed<TResult>(error);
        }
        catch (FileNotFoundException exception)
        {
            return ExpectedFailure<TResult>(
                operationName, FileErrorCodes.NotFound, exception.Message, exception.FileName);
        }
        catch (DirectoryNotFoundException exception)
        {
            return ExpectedFailure<TResult>(operationName, FileErrorCodes.DirectoryNotFound, exception.Message);
        }
        catch (UnauthorizedAccessException exception)
        {
            return ExpectedFailure<TResult>(operationName, FileErrorCodes.AccessDenied, exception.Message);
        }
        catch (IOException exception)
        {
            return ExpectedFailure<TResult>(operationName, FileErrorCodes.SystemFailure, exception.Message);
        }
        catch (NotSupportedException exception)
        {
            return ExpectedFailure<TResult>(
                operationName,
                RepositoryErrorCodes.UnsupportedVersion,
                exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            OperationErrorCode code = exception.InnerException is IOException
                ? RepositoryErrorCodes.OperationAlreadyRunning
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
            "Recovery operation {OperationName} failed with {ErrorCode}: {@Parameters}",
            operationName,
            code,
            parameters);

        return new OperationFailed<TResult>(error);
    }
}

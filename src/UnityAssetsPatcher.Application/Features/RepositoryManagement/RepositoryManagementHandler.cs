using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Application.Messaging;
using UnityAssetsPatcher.Application.Operations;
using UnityAssetsPatcher.Application.Repository;

namespace UnityAssetsPatcher.Application.Features.RepositoryManagement;

public sealed class RepositoryManagementHandler :
    IRequestHandler<ClearUnsupportedRepositoryRequest, OperationResult<RepositoryClearResult>>
{
    private readonly RepositoryService _repository;
    private readonly ILogger<RepositoryManagementHandler> _logger;

    public RepositoryManagementHandler(
        RepositoryService repository,
        ILogger<RepositoryManagementHandler>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(repository);

        _repository = repository;
        _logger = logger ?? NullLogger<RepositoryManagementHandler>.Instance;
    }

    public Task<OperationResult<RepositoryClearResult>> HandleAsync(
        ClearUnsupportedRepositoryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            RepositoryClearResult result = _repository.ClearUnsupportedRepository();
            _logger.LogInformation(
                "Cleared backup repository format {PreviousFormatVersion} and initialized format {FormatVersion}",
                result.PreviousFormatVersion,
                result.FormatVersion);

            return Task.FromResult<OperationResult<RepositoryClearResult>>(
                new OperationSucceeded<RepositoryClearResult>(result));
        }
        catch (RepositoryClearNotAllowedException exception)
        {
            return Task.FromResult<OperationResult<RepositoryClearResult>>(
                ExpectedFailure(RepositoryErrorCodes.ClearNotAllowed, exception));
        }
        catch (InvalidOperationException exception) when (exception.InnerException is IOException)
        {
            return Task.FromResult<OperationResult<RepositoryClearResult>>(
                ExpectedFailure(RepositoryErrorCodes.OperationAlreadyRunning, exception));
        }
        catch (InvalidDataException exception)
        {
            return Task.FromResult<OperationResult<RepositoryClearResult>>(
                ExpectedFailure(RepositoryErrorCodes.Unsafe, exception));
        }
        catch (UnauthorizedAccessException exception)
        {
            return Task.FromResult<OperationResult<RepositoryClearResult>>(
                ExpectedFailure(FileErrorCodes.AccessDenied, exception));
        }
        catch (IOException exception)
        {
            return Task.FromResult<OperationResult<RepositoryClearResult>>(
                ExpectedFailure(FileErrorCodes.SystemFailure, exception));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Clearing the unsupported backup repository failed");

            throw;
        }
    }

    private OperationFailed<RepositoryClearResult> ExpectedFailure(
        OperationErrorCode code,
        Exception exception)
    {
        var parameters = new Dictionary<string, object?>
        {
            ["detail"] = exception.Message,
            ["path"] = _repository.RepositoryDirectory
        };
        _logger.LogWarning(
            "Clearing the unsupported backup repository failed with {ErrorCode}: {@Parameters}",
            code,
            parameters);

        return new OperationFailed<RepositoryClearResult>(new OperationError(code, parameters));
    }
}

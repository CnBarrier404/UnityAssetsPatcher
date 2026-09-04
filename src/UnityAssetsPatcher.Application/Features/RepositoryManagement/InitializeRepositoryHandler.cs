using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Messaging;
using UnityAssetsPatcher.Application.Operations;
using UnityAssetsPatcher.Application.Repository;

namespace UnityAssetsPatcher.Application.Features.RepositoryManagement;

public sealed class InitializeRepositoryHandler :
    IRequestHandler<InitializeRepositoryRequest, OperationResult<RepositoryRecoveryReport>>
{
    private readonly IRepository _repository;

    public InitializeRepositoryHandler(IRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);

        _repository = repository;
    }

    public Task<OperationResult<RepositoryRecoveryReport>> HandleAsync(
        InitializeRepositoryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            _repository.Initialize();

            return Task.FromResult<OperationResult<RepositoryRecoveryReport>>(
                new OperationSucceeded<RepositoryRecoveryReport>(RepositoryRecoveryReport.Clean));
        }
        catch (RepositoryRecoveryException exception)
        {
            var error = new OperationError(
                RepositoryErrorCodes.RecoveryRequired,
                recovery: exception.Recovery);

            return Task.FromResult<OperationResult<RepositoryRecoveryReport>>(
                new OperationFailed<RepositoryRecoveryReport>(error));
        }
        catch (UnsupportedRepositoryFormatException exception)
        {
            var error = new OperationError(
                RepositoryErrorCodes.UnsupportedVersion,
                new Dictionary<string, object?>
                {
                    ["actual"] = exception.ActualVersion,
                    ["supported"] = exception.SupportedVersion,
                    ["detail"] = exception.Message
                });

            return Task.FromResult<OperationResult<RepositoryRecoveryReport>>(
                new OperationFailed<RepositoryRecoveryReport>(error));
        }
        catch (NotSupportedException exception)
        {
            var error = new OperationError(
                RepositoryErrorCodes.UnsupportedVersion,
                new Dictionary<string, object?> { ["detail"] = exception.Message });

            return Task.FromResult<OperationResult<RepositoryRecoveryReport>>(
                new OperationFailed<RepositoryRecoveryReport>(error));
        }
        catch (InvalidOperationException exception)
        {
            OperationErrorCode code = exception.InnerException is IOException
                ? RepositoryErrorCodes.OperationAlreadyRunning
                : RepositoryErrorCodes.Unsafe;
            var error = new OperationError(
                code,
                new Dictionary<string, object?> { ["detail"] = exception.Message });

            return Task.FromResult<OperationResult<RepositoryRecoveryReport>>(
                new OperationFailed<RepositoryRecoveryReport>(error));
        }
    }
}

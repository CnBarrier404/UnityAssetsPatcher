using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Operations;

namespace UnityAssetsPatcher.Application.Repository;

public sealed class RepositoryInitializationModule
{
    private readonly IRepository _repository;

    public RepositoryInitializationModule(IRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);

        _repository = repository;
    }

    public OperationResult<RepositoryRecoveryReport> Initialize()
    {
        try
        {
            _repository.Initialize();

            return new OperationSucceeded<RepositoryRecoveryReport>(RepositoryRecoveryReport.Clean);
        }
        catch (RepositoryRecoveryException exception)
        {
            var error = new OperationError(RepositoryErrorCodes.RecoveryRequired, recovery: exception.Recovery);

            return new OperationFailed<RepositoryRecoveryReport>(error);
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

            return new OperationFailed<RepositoryRecoveryReport>(error);
        }
        catch (NotSupportedException exception)
        {
            var error = new OperationError(
                RepositoryErrorCodes.UnsupportedVersion,
                new Dictionary<string, object?> { ["detail"] = exception.Message });

            return new OperationFailed<RepositoryRecoveryReport>(error);
        }
        catch (InvalidOperationException exception)
        {
            OperationErrorCode code = exception.InnerException is IOException
                ? RepositoryErrorCodes.OperationAlreadyRunning
                : RepositoryErrorCodes.Unsafe;

            var error = new OperationError(
                code,
                new Dictionary<string, object?> { ["detail"] = exception.Message });

            return new OperationFailed<RepositoryRecoveryReport>(error);
        }
    }
}

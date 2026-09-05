using System.Text.Json;
using Microsoft.Extensions.Logging;
using UnityAssetsPatcher.Application.Operations;

namespace UnityAssetsPatcher.Application.Updates;

public sealed class UpdateCheckModule
{
    private readonly IUpdateChecker _updateChecker;
    private readonly ILogger<UpdateCheckModule> _logger;

    public UpdateCheckModule(IUpdateChecker updateChecker, ILogger<UpdateCheckModule> logger)
    {
        ArgumentNullException.ThrowIfNull(updateChecker);
        ArgumentNullException.ThrowIfNull(logger);

        _updateChecker = updateChecker;
        _logger = logger;
    }

    public async Task<OperationResult<UpdateInfo?>> CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            UpdateInfo? update = await _updateChecker
                .CheckForUpdateAsync(cancellationToken)
                .ConfigureAwait(false);

            return new OperationSucceeded<UpdateInfo?>(update);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException exception)
        {
            if (exception.StatusCode is { } statusCode)
            {
                UpdateLog.UpdateRequestRejected(_logger, exception, (int)statusCode);
            }
            else
            {
                UpdateLog.UpdateRequestFailed(_logger, exception, exception.Message);
            }

            return Failure(exception.Message);
        }
        catch (JsonException exception)
        {
            UpdateLog.UpdateManifestRejectedAsInvalidJson(_logger, exception);

            return Failure(exception.Message);
        }
        catch (InvalidDataException exception)
        {
            UpdateLog.UpdateManifestRejected(_logger, exception, exception.Message);

            return Failure(exception.Message);
        }
        catch (IOException exception)
        {
            UpdateLog.UpdateRequestFailed(_logger, exception, exception.Message);

            return Failure(exception.Message);
        }
        catch (OperationCanceledException exception)
        {
            return Failure(exception.Message);
        }
    }

    private static OperationFailed<UpdateInfo?> Failure(string? detail)
    {
        IReadOnlyDictionary<string, object?>? parameters = string.IsNullOrWhiteSpace(detail)
            ? null
            : new Dictionary<string, object?> { ["detail"] = detail };

        return new OperationFailed<UpdateInfo?>(new OperationError(UpdateErrorCodes.CheckFailed, parameters));
    }
}

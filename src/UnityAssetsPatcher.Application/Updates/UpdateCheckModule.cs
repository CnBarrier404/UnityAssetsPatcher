using System.Text.Json;
using Microsoft.Extensions.Logging;
using UnityAssetsPatcher.Application.Operations;

namespace UnityAssetsPatcher.Application.Updates;

public sealed class UpdateCheckModule
{
    private readonly IUpdateChecker _updateChecker;

    public UpdateCheckModule(IUpdateChecker updateChecker, ILogger<UpdateCheckModule> logger)
    {
        ArgumentNullException.ThrowIfNull(updateChecker);
        ArgumentNullException.ThrowIfNull(logger);

        _updateChecker = updateChecker;
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
            return Failure(exception.Message);
        }
        catch (JsonException exception)
        {
            return Failure(exception.Message);
        }
        catch (IOException exception)
        {
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

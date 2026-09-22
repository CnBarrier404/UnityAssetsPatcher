using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace UnityAssetsPatcher.Application.Updates;

public sealed class UpdateCheckService
{
    private readonly IUpdateChecker _releaseClient;
    private readonly string _currentVersion;
    private readonly ILogger<UpdateCheckService> _logger;

    public UpdateCheckService(IUpdateChecker releaseClient, string currentVersion, ILogger<UpdateCheckService> logger)
    {
        ArgumentNullException.ThrowIfNull(releaseClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentVersion);
        ArgumentNullException.ThrowIfNull(logger);

        _releaseClient = releaseClient;
        _currentVersion = currentVersion;
        _logger = logger;
    }

    public async Task<UpdateInfo?> CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!SemanticVersion.TryParse(_currentVersion, out SemanticVersion currentVersion))
        {
            UpdateLog.UpdateCheckSkipped(_logger);

            return null;
        }

        UpdateLog.UpdateCheckStarted(_logger);

        try
        {
            UpdateInfo release = await _releaseClient
                .CheckForUpdateAsync(cancellationToken)
                .ConfigureAwait(false);

            if (!SemanticVersion.TryParse(release.Version, out SemanticVersion latestVersion))
            {
                throw new InvalidDataException("The update release contains an invalid version.");
            }

            if (latestVersion.CompareTo(currentVersion) <= 0)
            {
                UpdateLog.UpdateCheckCompletedWithoutUpdate(_logger);

                return null;
            }

            UpdateLog.UpdateCheckCompletedWithUpdate(
                _logger,
                _currentVersion,
                release.Version);

            return release;
        }
        catch (HttpRequestException exception)
        {
            if (exception.StatusCode is { } statusCode)
            {
                string reason = statusCode switch
                {
                    HttpStatusCode.TooManyRequests => "API rate limit exceeded",
                    HttpStatusCode.Forbidden => "request forbidden; HTTP 403 alone does not identify the cause",
                    HttpStatusCode.Unauthorized => "authentication required or rejected",
                    HttpStatusCode.NotFound => "repository or latest release not found or not accessible",
                    >= HttpStatusCode.InternalServerError => "server error",
                    _ => "unsuccessful HTTP response"
                };
                UpdateLog.UpdateRequestRejected(_logger, exception, (int)statusCode, reason);
            }
            else
            {
                UpdateLog.UpdateRequestFailed(_logger, exception, exception.HttpRequestError, exception.Message);
            }

            throw new UpdateCheckException(exception);
        }
        catch (JsonException exception)
        {
            UpdateLog.UpdateResponseRejectedAsInvalidJson(_logger, exception);

            throw new UpdateCheckException(exception);
        }
        catch (InvalidDataException exception)
        {
            UpdateLog.UpdateResponseRejected(_logger, exception, exception.Message);

            throw new UpdateCheckException(exception);
        }
        catch (IOException exception)
        {
            UpdateLog.UpdateResponseReadFailed(_logger, exception, exception.Message);

            throw new UpdateCheckException(exception);
        }
        catch (OperationCanceledException exception) when (exception.InnerException is TimeoutException)
        {
            UpdateLog.UpdateRequestTimedOut(_logger, exception);

            throw new UpdateCheckException(exception);
        }
    }
}

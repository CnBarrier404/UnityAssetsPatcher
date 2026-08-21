using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using UnityAssetsPatcher.Application;
using UnityAssetsPatcher.Application.Updates;

namespace UnityAssetsPatcher.Infrastructure.Updates;

public sealed class GitHubUpdateChecker : IUpdateChecker
{
    private readonly HttpClient _httpClient;
    private readonly AppInfo _appInfo;
    private readonly ILogger<GitHubUpdateChecker> _logger;

    public const string UpdateManifestUrl =
        "https://github.com/CnBarrier404/UnityAssetsPatcher/releases/latest/download/update.json";

    public const int MaximumManifestSize = 64 * 1024;

    public GitHubUpdateChecker(
        HttpClient httpClient,
        AppInfo appInfo,
        ILogger<GitHubUpdateChecker> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(appInfo);
        ArgumentNullException.ThrowIfNull(logger);

        _httpClient = httpClient;
        _appInfo = appInfo;
        _logger = logger;
    }

    public async Task<UpdateCheckResult> CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!SemanticVersion.TryParse(_appInfo.DisplayVersion, out SemanticVersion currentVersion))
        {
            UpdateLog.UpdateCheckSkipped(_logger, _appInfo.DisplayVersion);

            return new UpdateCheckFailed();
        }

        try
        {
            UpdateLog.CheckingForUpdate(_logger, UpdateManifestUrl);

            using HttpRequestMessage request = CreateRequest();
            using HttpResponseMessage response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                UpdateLog.UpdateRequestRejected(_logger, (int)response.StatusCode);

                return new UpdateCheckFailed();
            }

            UpdateManifest? manifest = await UpdateManifestReader.ReadAsync(
                response.Content,
                MaximumManifestSize,
                cancellationToken).ConfigureAwait(false);

            if (manifest is null)
            {
                UpdateLog.UpdateManifestRejected(_logger);

                return new UpdateCheckFailed();
            }

            if (manifest.SemanticVersion.CompareTo(currentVersion) <= 0)
            {
                UpdateLog.NoUpdateAvailable(_logger, _appInfo.DisplayVersion, manifest.Version);

                return new UpToDate();
            }

            UpdateLog.UpdateAvailable(_logger, _appInfo.DisplayVersion, manifest.Version);

            return new UpdateAvailable(new AvailableUpdate(
                manifest.Version,
                manifest.ReleaseUrl,
                manifest.DownloadUrl,
                manifest.Sha256));
        }
        catch (HttpRequestException exception)
        {
            UpdateLog.UpdateRequestFailed(_logger, exception);

            throw;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            UpdateLog.UpdateCheckCanceled(_logger);

            throw;
        }
        catch (OperationCanceledException exception)
        {
            UpdateLog.UpdateRequestFailed(_logger, exception);

            throw;
        }
        catch (IOException exception)
        {
            UpdateLog.UpdateRequestFailed(_logger, exception);

            throw;
        }
        catch (JsonException)
        {
            UpdateLog.UpdateManifestRejected(_logger);

            throw;
        }
    }

    private static HttpRequestMessage CreateRequest()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, UpdateManifestUrl);

        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        request.Headers.UserAgent.ParseAdd("UnityAssetsPatcher");

        return request;
    }
}

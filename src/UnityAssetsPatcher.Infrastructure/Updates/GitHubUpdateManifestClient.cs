using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace UnityAssetsPatcher.Infrastructure.Updates;

internal sealed class GitHubUpdateManifestClient
{
    internal const string UpdateManifestUrl =
        "https://github.com/CnBarrier404/UnityAssetsPatcher/releases/latest/download/update.json";

    internal const int MaximumManifestSize = 64 * 1024;

    private readonly HttpClient _httpClient;
    private readonly ILogger<GitHubUpdateManifestClient> _logger;

    public GitHubUpdateManifestClient(HttpClient httpClient, ILogger<GitHubUpdateManifestClient> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(logger);

        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<UpdateManifest?> FetchAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            return await FetchLatestManifestAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException exception)
        {
            UpdateLog.UpdateRequestFailed(_logger, exception, exception.Message);

            throw;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            UpdateLog.UpdateCheckCanceled(_logger);

            throw;
        }
        catch (OperationCanceledException exception)
        {
            UpdateLog.UpdateRequestFailed(_logger, exception, exception.Message);

            throw;
        }
        catch (IOException exception)
        {
            UpdateLog.UpdateRequestFailed(_logger, exception, exception.Message);

            throw;
        }
        catch (JsonException exception)
        {
            UpdateLog.UpdateManifestRejectedAsInvalidJson(_logger, exception);

            throw;
        }
    }

    private async Task<UpdateManifest?> FetchLatestManifestAsync(CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = CreateRequest();
        using HttpResponseMessage response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            UpdateLog.UpdateRequestRejected(_logger, (int)response.StatusCode);

            return null;
        }

        if (response.Content.Headers.ContentLength is > MaximumManifestSize)
        {
            UpdateLog.UpdateManifestRejectedAsTooLarge(_logger, MaximumManifestSize);

            return null;
        }

        await using Stream contentStream = await response.Content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);

        UpdateManifestReadResult readResult = await UpdateManifestReader.ReadAsync(
            contentStream,
            MaximumManifestSize,
            cancellationToken).ConfigureAwait(false);

        if (readResult.Status is UpdateManifestReadStatus.TooLarge)
        {
            UpdateLog.UpdateManifestRejectedAsTooLarge(_logger, MaximumManifestSize);
        }
        else if (readResult.Status is UpdateManifestReadStatus.Invalid)
        {
            UpdateLog.UpdateManifestRejected(_logger);
        }

        return readResult.Manifest;
    }

    private static HttpRequestMessage CreateRequest()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, UpdateManifestUrl);

        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        request.Headers.UserAgent.ParseAdd("UnityAssetsPatcher");

        return request;
    }
}

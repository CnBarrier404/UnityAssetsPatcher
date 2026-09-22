using System.Net.Http.Headers;
using UnityAssetsPatcher.Application;
using UnityAssetsPatcher.Application.Updates;

namespace UnityAssetsPatcher.Infrastructure.Updates;

internal sealed class GitHubUpdateClient : IUpdateChecker
{
    internal const string LatestReleaseUrl =
        "https://api.github.com/repos/CnBarrier404/UnityAssetsPatcher/releases/latest";

    internal const int MaximumResponseSize = 1024 * 1024;

    private readonly HttpClient _httpClient;

    public GitHubUpdateClient(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);

        _httpClient = httpClient;
    }

    public async Task<UpdateInfo> CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using HttpRequestMessage request = CreateRequest();
        using HttpResponseMessage response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        if (response.Content.Headers.ContentLength is > MaximumResponseSize)
        {
            throw new InvalidDataException(
                $"The GitHub release response exceeds the maximum size of {MaximumResponseSize} bytes.");
        }

        await using Stream contentStream = await response.Content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);

        GitHubUpdateReadResult readResult = await GitHubUpdateReader.ReadAsync(
            contentStream,
            MaximumResponseSize,
            cancellationToken).ConfigureAwait(false);

        switch (readResult.Status)
        {
            case GitHubUpdateReadStatus.TooLarge:
                throw new InvalidDataException(
                    $"The GitHub release response exceeds the maximum size of {MaximumResponseSize} bytes.");
            case GitHubUpdateReadStatus.Invalid:
                throw new InvalidDataException("The GitHub release response does not match the expected format.");
            case GitHubUpdateReadStatus.Success:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        return readResult.Release ?? throw new InvalidOperationException(
            "The GitHub release response reader returned no release for a successful result.");
    }

    private static HttpRequestMessage CreateRequest()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseUrl);

        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        request.Headers.UserAgent.ParseAdd(AppConfig.Name);

        return request;
    }
}

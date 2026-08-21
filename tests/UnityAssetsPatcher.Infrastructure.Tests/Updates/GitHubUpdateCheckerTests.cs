using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using UnityAssetsPatcher.Application;
using UnityAssetsPatcher.Application.Updates;
using UnityAssetsPatcher.Infrastructure.Updates;
using Xunit;

namespace UnityAssetsPatcher.Infrastructure.Tests.Updates;

public sealed class GitHubUpdateCheckerTests
{
    private const string Sha256 = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [Fact]
    public async Task CheckForUpdateAsync_WhenLatestReleaseIsNewer_ReturnsUpdateInfo()
    {
        var handler = new StubHttpMessageHandler(_ => CreateJsonResponse(Manifest("v1.3.0")));
        using HttpClient httpClient = new(handler);
        GitHubUpdateChecker checker = CreateChecker(httpClient, "v1.2.3");

        UpdateInfo? update = await Check(checker);

        Assert.NotNull(update);
        Assert.Equal("v1.3.0", update.Version);
        Assert.Equal(1, handler.RequestCount);
    }

    [Theory]
    [InlineData("v1.2.3", "v1.2.3")]
    [InlineData("v1.2.3", "v1.2.2")]
    [InlineData("v2.0.0", "v1.9.9")]
    [InlineData("v1.2.3-beta.2", "v1.2.3-beta.1")]
    public async Task CheckForUpdateAsync_WhenReleaseIsNotNewer_ReturnsNull(
        string currentVersion,
        string latestVersion)
    {
        using HttpClient httpClient = new(
            new StubHttpMessageHandler(_ => CreateJsonResponse(Manifest(latestVersion))));
        GitHubUpdateChecker checker = CreateChecker(httpClient, currentVersion);

        Assert.Null(await Check(checker));
    }

    [Theory]
    [InlineData("v1.2.3-beta.2", "v1.2.3")]
    [InlineData("v1.2.3-beta.2", "v1.2.3-beta.10")]
    [InlineData("v1.2.3-alpha", "v1.2.3-beta")]
    public async Task CheckForUpdateAsync_WhenPrereleaseOrderingMakesReleaseNewer_ReturnsUpdate(
        string currentVersion,
        string latestVersion)
    {
        using HttpClient httpClient = new(
            new StubHttpMessageHandler(_ => CreateJsonResponse(Manifest(latestVersion))));
        GitHubUpdateChecker checker = CreateChecker(httpClient, currentVersion);

        UpdateInfo? update = await Check(checker);

        Assert.NotNull(update);
        Assert.Equal(latestVersion, update.Version);
    }

    [Fact]
    public async Task CheckForUpdateAsync_WhenRunningDevelopmentBuild_ReturnsNullWithoutRequestingManifest()
    {
        var handler = new StubHttpMessageHandler(_ => CreateJsonResponse(Manifest("v1.3.0")));
        using HttpClient httpClient = new(handler);
        GitHubUpdateChecker checker = CreateChecker(httpClient, "dev");

        Assert.Null(await Check(checker));
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task CheckForUpdateAsync_WhenManifestVersionIsInvalid_ThrowsInvalidDataException()
    {
        using HttpClient httpClient = new(
            new StubHttpMessageHandler(_ => CreateJsonResponse(Manifest("invalid"))));
        GitHubUpdateChecker checker = CreateChecker(httpClient, "v1.2.3");

        await Assert.ThrowsAsync<InvalidDataException>(() => Check(checker));
    }

    [Fact]
    public async Task CheckForUpdateAsync_WhenRequestFails_PropagatesHttpRequestException()
    {
        var exception = new HttpRequestException("Offline");
        using HttpClient httpClient = new(new StubHttpMessageHandler(_ => throw exception));
        GitHubUpdateChecker checker = CreateChecker(httpClient, "v1.2.3");

        var actual = await Assert.ThrowsAsync<HttpRequestException>(() => Check(checker));

        Assert.Same(exception, actual);
    }

    private static GitHubUpdateChecker CreateChecker(HttpClient httpClient, string currentVersion)
    {
        var manifestClient = new GitHubUpdateManifestClient(
            httpClient,
            NullLogger<GitHubUpdateManifestClient>.Instance);

        return new GitHubUpdateChecker(
            manifestClient,
            new AppInfo("Unity Assets Patcher", currentVersion),
            NullLogger<GitHubUpdateChecker>.Instance);
    }

    private static Task<UpdateInfo?> Check(GitHubUpdateChecker checker)
    {
        return checker.CheckForUpdateAsync(TestContext.Current.CancellationToken);
    }

    private static string Manifest(string version)
    {
        return $$"""
                 {
                   "schemaVersion": 1,
                   "version": "{{version}}",
                   "releaseUrl": "https://example.com/releases/{{version}}",
                   "downloadUrl": "https://example.com/download/{{version}}",
                   "sha256": "{{Sha256}}"
                 }
                 """;
    }

    private static HttpResponseMessage CreateJsonResponse(string json)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json)
        };
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public int RequestCount { get; private set; }

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;

            return Task.FromResult(_handler(request));
        }
    }
}

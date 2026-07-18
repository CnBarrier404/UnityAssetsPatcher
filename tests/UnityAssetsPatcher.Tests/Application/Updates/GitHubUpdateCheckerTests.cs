using System.Net;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Updates;
using UnityAssetsPatcher.Application;
using Xunit;

namespace UnityAssetsPatcher.Tests.Application.Updates;

public sealed class GitHubUpdateCheckerTests
{
    private const string Sha256 = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [Fact]
    public async Task CheckForUpdateAsync_WhenLatestReleaseIsNewer_ReturnsReleaseDetails()
    {
        var handler = new StubHttpMessageHandler(_ => CreateJsonResponse(Manifest("v1.3.0")));
        using var httpClient = new HttpClient(handler);
        var checker = new GitHubUpdateChecker(
            httpClient,
            new AppInfo("Unity Assets Patcher", "v1.2.3"));

        UpdateAvailable result = Assert.IsType<UpdateAvailable>(await Check(checker));

        Assert.Equal("v1.3.0", result.Update.Version);
        Assert.Equal("https://example.com/releases/v1.3.0", result.Update.ReleaseUrl.AbsoluteUri);
        Assert.Equal("https://example.com/download/UnityAssetsPatcher-v1.3.0-win-x64.exe", result.Update.DownloadUrl.AbsoluteUri);
        Assert.Equal(Sha256, result.Update.Sha256);
        Assert.Equal(1, handler.RequestCount);
        Assert.Equal(GitHubUpdateChecker.UpdateManifestUrl, handler.LastRequestUri?.AbsoluteUri);
        Assert.Contains("UnityAssetsPatcher", handler.LastUserAgent ?? string.Empty);
    }

    [Theory]
    [InlineData("v1.2.3", "v1.2.3")]
    [InlineData("v1.2.3", "v1.2.2")]
    [InlineData("v2.0.0", "v1.9.9")]
    public async Task CheckForUpdateAsync_WhenReleaseIsNotNewer_ReturnsUpToDate(
        string currentVersion,
        string latestVersion)
    {
        using var httpClient = new HttpClient(
            new StubHttpMessageHandler(_ => CreateJsonResponse(Manifest(latestVersion))));
        var checker = new GitHubUpdateChecker(
            httpClient,
            new AppInfo("Unity Assets Patcher", currentVersion));

        Assert.IsType<UpToDate>(await Check(checker));
    }

    [Fact]
    public async Task CheckForUpdateAsync_WhenStableReleaseReplacesCurrentPrerelease_ReturnsUpdate()
    {
        using var httpClient = new HttpClient(
            new StubHttpMessageHandler(_ => CreateJsonResponse(Manifest("v1.2.3"))));
        var checker = new GitHubUpdateChecker(
            httpClient,
            new AppInfo("Unity Assets Patcher", "v1.2.3-beta.2"));

        UpdateAvailable result = Assert.IsType<UpdateAvailable>(await Check(checker));

        Assert.Equal("v1.2.3", result.Update.Version);
    }

    [Fact]
    public async Task CheckForUpdateAsync_NormalizesSha256ToLowercase()
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ =>
            CreateJsonResponse(Manifest("v1.3.0", sha256: Sha256.ToUpperInvariant()))));
        var checker = new GitHubUpdateChecker(
            httpClient,
            new AppInfo("Unity Assets Patcher", "v1.2.3"));

        UpdateAvailable result = Assert.IsType<UpdateAvailable>(await Check(checker));

        Assert.Equal(Sha256, result.Update.Sha256);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("not-json")]
    [InlineData(
        "{\"schemaVersion\":2,\"version\":\"v1.3.0\",\"releaseUrl\":\"https://example.com\",\"downloadUrl\":\"https://example.com/file.zip\",\"sha256\":\"0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef\"}")]
    [InlineData(
        "{\"schemaVersion\":1,\"version\":\"invalid\",\"releaseUrl\":\"https://example.com\",\"downloadUrl\":\"https://example.com/file.zip\",\"sha256\":\"0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef\"}")]
    [InlineData(
        "{\"schemaVersion\":1,\"version\":\"v1.3.0\",\"releaseUrl\":\"http://example.com\",\"downloadUrl\":\"https://example.com/file.zip\",\"sha256\":\"0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef\"}")]
    [InlineData(
        "{\"schemaVersion\":1,\"version\":\"v1.3.0\",\"releaseUrl\":\"https://example.com\",\"downloadUrl\":\"https://example.com/file.zip\",\"sha256\":\"invalid\"}")]
    public async Task CheckForUpdateAsync_WhenManifestIsInvalid_ReturnsFailed(string json)
    {
        using var httpClient = new HttpClient(
            new StubHttpMessageHandler(_ => CreateJsonResponse(json)));
        var checker = new GitHubUpdateChecker(
            httpClient,
            new AppInfo("Unity Assets Patcher", "v1.2.3"));

        Assert.IsType<UpdateCheckFailed>(await Check(checker));
    }

    [Fact]
    public async Task CheckForUpdateAsync_WhenManifestIsTooLarge_ReturnsFailed()
    {
        string json = new(' ', GitHubUpdateChecker.MaximumManifestSize + 1);
        using var httpClient = new HttpClient(
            new StubHttpMessageHandler(_ => CreateJsonResponse(json)));
        var checker = new GitHubUpdateChecker(
            httpClient,
            new AppInfo("Unity Assets Patcher", "v1.2.3"));

        Assert.IsType<UpdateCheckFailed>(await Check(checker));
    }

    [Fact]
    public async Task CheckForUpdateAsync_WhenRunningDevelopmentBuild_DoesNotSendRequest()
    {
        var handler = new StubHttpMessageHandler(_ => throw new InvalidOperationException());
        using var httpClient = new HttpClient(handler);
        var checker = new GitHubUpdateChecker(
            httpClient,
            new AppInfo("Unity Assets Patcher", "dev"));

        Assert.IsType<UpdateCheckFailed>(await Check(checker));
        Assert.Equal(0, handler.RequestCount);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task CheckForUpdateAsync_WhenRequestIsNotSuccessful_ReturnsFailed(HttpStatusCode statusCode)
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(statusCode)));
        var checker = new GitHubUpdateChecker(
            httpClient,
            new AppInfo("Unity Assets Patcher", "v1.2.3"));

        Assert.IsType<UpdateCheckFailed>(await Check(checker));
    }

    [Fact]
    public async Task CheckForUpdateAsync_WhenRequestFails_ReturnsFailed()
    {
        using var httpClient = new HttpClient(
            new StubHttpMessageHandler(_ => throw new HttpRequestException("Offline")));
        var checker = new GitHubUpdateChecker(
            httpClient,
            new AppInfo("Unity Assets Patcher", "v1.2.3"));

        Assert.IsType<UpdateCheckFailed>(await Check(checker));
    }

    [Fact]
    public async Task CheckForUpdateAsync_WhenCanceled_ReturnsFailed()
    {
        using var httpClient = new HttpClient(
            new StubHttpMessageHandler(_ => throw new OperationCanceledException()));
        var checker = new GitHubUpdateChecker(
            httpClient,
            new AppInfo("Unity Assets Patcher", "v1.2.3"));

        Assert.IsType<UpdateCheckFailed>(await Check(checker));
    }

    private static string Manifest(string version, string sha256 = Sha256)
    {
        return $$"""
                 {
                   "schemaVersion": 1,
                   "version": "{{version}}",
                   "releaseUrl": "https://example.com/releases/{{version}}",
                   "downloadUrl": "https://example.com/download/UnityAssetsPatcher-{{version}}-win-x64.exe",
                   "sha256": "{{sha256}}",
                   "ignored": true
                 }
                 """;
    }

    private static HttpResponseMessage CreateJsonResponse(string json)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json),
        };
    }

    private static Task<UpdateCheckResult> Check(GitHubUpdateChecker checker)
    {
        return checker.CheckForUpdateAsync(TestContext.Current.CancellationToken);
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        public Uri? LastRequestUri { get; private set; }

        public string? LastUserAgent { get; private set; }

        protected override HttpResponseMessage Send(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            LastRequestUri = request.RequestUri;
            LastUserAgent = request.Headers.UserAgent.ToString();
            return handler(request);
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(Send(request, cancellationToken));
        }
    }
}

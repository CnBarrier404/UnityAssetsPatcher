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
    public async Task CheckForUpdateAsync_WhenLatestReleaseIsNewer_ReturnsReleaseDetails()
    {
        var handler = new StubHttpMessageHandler(_ => CreateJsonResponse(Manifest("v1.3.0")));
        using HttpClient httpClient = new(handler);
        GitHubUpdateChecker checker = CreateChecker(httpClient, "v1.2.3");

        var result = Assert.IsType<UpdateAvailable>(await Check(checker));

        Assert.Equal("v1.3.0", result.Update.Version);
        Assert.Equal("https://example.com/releases/v1.3.0", result.Update.ReleaseUrl.AbsoluteUri);
        Assert.Equal(
            "https://example.com/download/UnityAssetsPatcher-v1.3.0-win-x64.exe",
            result.Update.DownloadUrl.AbsoluteUri);
        Assert.Equal(Sha256, result.Update.Sha256);
        Assert.Equal(1, handler.RequestCount);
        Assert.Equal(GitHubUpdateChecker.UpdateManifestUrl, handler.LastRequestUri?.AbsoluteUri);
        Assert.Contains("UnityAssetsPatcher", handler.LastUserAgent ?? string.Empty);
        Assert.Contains("application/json", handler.LastAccept ?? string.Empty);
    }

    [Theory]
    [InlineData("v1.2.3", "v1.2.3")]
    [InlineData("v1.2.3", "v1.2.2")]
    [InlineData("v2.0.0", "v1.9.9")]
    [InlineData("v1.2.3-beta.2", "v1.2.3-beta.1")]
    public async Task CheckForUpdateAsync_WhenReleaseIsNotNewer_ReturnsUpToDate(
        string currentVersion,
        string latestVersion)
    {
        using HttpClient httpClient = new(
            new StubHttpMessageHandler(_ => CreateJsonResponse(Manifest(latestVersion))));
        GitHubUpdateChecker checker = CreateChecker(httpClient, currentVersion);

        Assert.IsType<UpToDate>(await Check(checker));
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

        var result = Assert.IsType<UpdateAvailable>(await Check(checker));

        Assert.Equal(latestVersion, result.Update.Version);
    }

    [Fact]
    public async Task CheckForUpdateAsync_WhenSha256ContainsUppercase_NormalizesSha256ToLowercase()
    {
        using HttpClient httpClient = new(new StubHttpMessageHandler(_ =>
            CreateJsonResponse(Manifest("v1.3.0", Sha256.ToUpperInvariant()))));
        GitHubUpdateChecker checker = CreateChecker(httpClient, "v1.2.3");

        var result = Assert.IsType<UpdateAvailable>(await Check(checker));

        Assert.Equal(Sha256, result.Update.Sha256);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("not-json")]
    [InlineData(
        """
        {
          "schemaVersion": 2,
          "version": "v1.3.0",
          "releaseUrl": "https://example.com",
          "downloadUrl": "https://example.com/file.exe",
          "sha256": "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"
        }
        """)]
    [InlineData(
        """
        {
          "schemaVersion": 1,
          "version": "invalid",
          "releaseUrl": "https://example.com",
          "downloadUrl": "https://example.com/file.exe",
          "sha256": "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"
        }
        """)]
    [InlineData(
        """
        {
          "schemaVersion": 1,
          "version": "v1.3.0",
          "releaseUrl": "http://example.com",
          "downloadUrl": "https://example.com/file.exe",
          "sha256": "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"
        }
        """)]
    [InlineData(
        """
        {
          "schemaVersion": 1,
          "version": "v1.3.0",
          "releaseUrl": "https://example.com",
          "downloadUrl": "http://example.com/file.exe",
          "sha256": "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"
        }
        """)]
    [InlineData(
        """
        {
          "schemaVersion": 1,
          "version": "v1.3.0",
          "releaseUrl": "https://example.com",
          "downloadUrl": "https://example.com/file.exe",
          "sha256": "invalid"
        }
        """)]
    public async Task CheckForUpdateAsync_WhenManifestIsInvalid_ReturnsFailed(string json)
    {
        using HttpClient httpClient = new(
            new StubHttpMessageHandler(_ => CreateJsonResponse(json)));
        GitHubUpdateChecker checker = CreateChecker(httpClient, "v1.2.3");

        Assert.IsType<UpdateCheckFailed>(await Check(checker));
    }

    [Fact]
    public async Task CheckForUpdateAsync_WhenContentLengthExceedsLimit_ReturnsFailed()
    {
        using HttpClient httpClient = new(new StubHttpMessageHandler(_ =>
        {
            HttpResponseMessage response = CreateJsonResponse("{}");
            response.Content.Headers.ContentLength = GitHubUpdateChecker.MaximumManifestSize + 1;

            return response;
        }));
        GitHubUpdateChecker checker = CreateChecker(httpClient, "v1.2.3");

        Assert.IsType<UpdateCheckFailed>(await Check(checker));
    }

    [Fact]
    public async Task CheckForUpdateAsync_WhenStreamExceedsLimitWithoutContentLength_ReturnsFailed()
    {
        string json = new(' ', GitHubUpdateChecker.MaximumManifestSize + 1);
        using HttpClient httpClient = new(new StubHttpMessageHandler(_ =>
        {
            HttpResponseMessage response = CreateJsonResponse(json);
            response.Content.Headers.ContentLength = null;

            return response;
        }));
        GitHubUpdateChecker checker = CreateChecker(httpClient, "v1.2.3");

        Assert.IsType<UpdateCheckFailed>(await Check(checker));
    }

    [Fact]
    public async Task CheckForUpdateAsync_WhenRunningDevelopmentBuild_DoesNotSendRequest()
    {
        var handler = new StubHttpMessageHandler(_ => throw new InvalidOperationException());
        using HttpClient httpClient = new(handler);
        GitHubUpdateChecker checker = CreateChecker(httpClient, "dev");

        Assert.IsType<UpdateCheckFailed>(await Check(checker));
        Assert.Equal(0, handler.RequestCount);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task CheckForUpdateAsync_WhenRequestIsNotSuccessful_ReturnsFailed(HttpStatusCode statusCode)
    {
        using HttpClient httpClient = new(
            new StubHttpMessageHandler(_ => new HttpResponseMessage(statusCode)));
        GitHubUpdateChecker checker = CreateChecker(httpClient, "v1.2.3");

        Assert.IsType<UpdateCheckFailed>(await Check(checker));
    }

    [Fact]
    public async Task CheckForUpdateAsync_WhenRequestFails_ReturnsFailed()
    {
        using HttpClient httpClient = new(
            new StubHttpMessageHandler(_ => throw new HttpRequestException("Offline")));
        GitHubUpdateChecker checker = CreateChecker(httpClient, "v1.2.3");

        Assert.IsType<UpdateCheckFailed>(await Check(checker));
    }

    [Fact]
    public async Task CheckForUpdateAsync_WhenCanceled_ReturnsFailed()
    {
        using HttpClient httpClient = new(
            new StubHttpMessageHandler(_ => throw new OperationCanceledException()));
        GitHubUpdateChecker checker = CreateChecker(httpClient, "v1.2.3");

        Assert.IsType<UpdateCheckFailed>(await Check(checker));
    }

    private static GitHubUpdateChecker CreateChecker(HttpClient httpClient, string currentVersion)
    {
        return new GitHubUpdateChecker(
            httpClient,
            new AppInfo("Unity Assets Patcher", currentVersion),
            NullLogger<GitHubUpdateChecker>.Instance);
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
            Content = new StringContent(json)
        };
    }

    private static Task<UpdateCheckResult> Check(GitHubUpdateChecker checker)
    {
        return checker.CheckForUpdateAsync(TestContext.Current.CancellationToken);
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public int RequestCount { get; private set; }

        public Uri? LastRequestUri { get; private set; }

        public string? LastUserAgent { get; private set; }

        public string? LastAccept { get; private set; }

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            LastRequestUri = request.RequestUri;
            LastUserAgent = request.Headers.UserAgent.ToString();
            LastAccept = request.Headers.Accept.ToString();

            return Task.FromResult(_handler(request));
        }
    }
}

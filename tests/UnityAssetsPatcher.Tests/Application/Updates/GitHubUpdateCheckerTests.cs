using System.Net;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Updates;
using UnityAssetsPatcher.Core;
using Xunit;

namespace UnityAssetsPatcher.Tests.Application.Updates;

public sealed class GitHubUpdateCheckerTests
{
    [Fact]
    public void CheckForUpdate_WhenLatestReleaseIsNewer_ReturnsReleaseDetails()
    {
        var handler = new StubHttpMessageHandler(_ => CreateJsonResponse(
            """
            {"tag_name":"v1.3.0","html_url":"https://github.com/CnBarrier404/UnityAssetsPatcher/releases/tag/v1.3.0"}
            """));
        using var httpClient = new HttpClient(handler);
        var checker = new GitHubUpdateChecker(
            httpClient,
            new AppInfo("Unity Assets Patcher", "v1.2.3"));

        AvailableUpdate? update = checker.CheckForUpdate();

        Assert.NotNull(update);
        Assert.Equal("v1.3.0", update.Version);
        Assert.Equal(
            "https://github.com/CnBarrier404/UnityAssetsPatcher/releases/tag/v1.3.0",
            update.ReleaseUrl.AbsoluteUri);
        Assert.Equal(1, handler.RequestCount);
        Assert.Equal(GitHubUpdateChecker.LatestReleaseUrl, handler.LastRequestUri?.AbsoluteUri);
        Assert.Contains("UnityAssetsPatcher", handler.LastUserAgent ?? string.Empty);
    }

    [Theory]
    [InlineData("v1.2.3", "v1.2.3")]
    [InlineData("v1.2.3", "v1.2.2")]
    [InlineData("v2.0.0", "v1.9.9")]
    [InlineData("v1.2.3", "not-a-version")]
    public void CheckForUpdate_WhenReleaseIsNotNewer_ReturnsNull(
        string currentVersion,
        string latestVersion)
    {
        var handler = new StubHttpMessageHandler(_ => CreateJsonResponse(
            $$"""
              {"tag_name":"{{latestVersion}}","html_url":"https://example.com/release"}
              """));
        using var httpClient = new HttpClient(handler);
        var checker = new GitHubUpdateChecker(
            httpClient,
            new AppInfo("Unity Assets Patcher", currentVersion));

        Assert.Null(checker.CheckForUpdate());
    }

    [Fact]
    public void CheckForUpdate_WhenStableReleaseReplacesCurrentPrerelease_ReturnsUpdate()
    {
        var handler = new StubHttpMessageHandler(_ => CreateJsonResponse(
            """
            {"tag_name":"v1.2.3","html_url":"https://example.com/release"}
            """));
        using var httpClient = new HttpClient(handler);
        var checker = new GitHubUpdateChecker(
            httpClient,
            new AppInfo("Unity Assets Patcher", "v1.2.3-beta.2"));

        AvailableUpdate? update = checker.CheckForUpdate();

        Assert.NotNull(update);
        Assert.Equal("v1.2.3", update.Version);
    }

    [Fact]
    public void CheckForUpdate_WhenRunningDevelopmentBuild_DoesNotSendRequest()
    {
        var handler = new StubHttpMessageHandler(_ => throw new InvalidOperationException());
        using var httpClient = new HttpClient(handler);
        var checker = new GitHubUpdateChecker(
            httpClient,
            new AppInfo("Unity Assets Patcher", "dev"));

        Assert.Null(checker.CheckForUpdate());
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public void CheckForUpdate_WhenGitHubRequestFails_ReturnsNull()
    {
        var handler = new StubHttpMessageHandler(_ => throw new HttpRequestException("Offline"));
        using var httpClient = new HttpClient(handler);
        var checker = new GitHubUpdateChecker(
            httpClient,
            new AppInfo("Unity Assets Patcher", "v1.2.3"));

        Assert.Null(checker.CheckForUpdate());
    }

    private static HttpResponseMessage CreateJsonResponse(string json)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json),
        };
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

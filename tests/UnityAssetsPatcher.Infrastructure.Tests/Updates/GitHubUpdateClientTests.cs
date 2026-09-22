using System.Net;
using System.Text.Json;
using UnityAssetsPatcher.Application.Updates;
using UnityAssetsPatcher.Infrastructure.Updates;
using Xunit;

namespace UnityAssetsPatcher.Infrastructure.Tests.Updates;

public sealed class GitHubUpdateClientTests
{
    [Fact]
    public async Task FetchAsync_WhenReleaseIsValid_ReturnsReleaseAndSendsExpectedRequest()
    {
        var handler = new StubHttpMessageHandler(_ => CreateJsonResponse(Release("v1.3.0")));
        using HttpClient httpClient = new(handler);
        GitHubUpdateClient client = CreateClient(httpClient);

        UpdateInfo release = await Fetch(client);

        Assert.NotNull(release);
        Assert.Equal("v1.3.0", release.Version);
        Assert.Equal("https://example.com/releases/v1.3.0", release.ReleaseUrl.AbsoluteUri);
        Assert.Equal(1, handler.RequestCount);
        Assert.Equal(GitHubUpdateClient.LatestReleaseUrl, handler.LastRequestUri?.AbsoluteUri);
        Assert.Contains("UnityAssetsPatcher", handler.LastUserAgent ?? string.Empty);
        Assert.Contains("application/vnd.github+json", handler.LastAccept ?? string.Empty);
        Assert.Equal("2022-11-28", handler.LastApiVersion);
    }

    [Fact]
    public async Task FetchAsync_WhenVersionIsNotSemantic_ReturnsReleaseForApplicationEvaluation()
    {
        using HttpClient httpClient = new(
            new StubHttpMessageHandler(_ => CreateJsonResponse(Release("invalid"))));
        GitHubUpdateClient client = CreateClient(httpClient);

        UpdateInfo release = await Fetch(client);

        Assert.NotNull(release);
        Assert.Equal("invalid", release.Version);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData(
        """
        {
          "tag_name": 1,
          "html_url": "https://example.com"
        }
        """)]
    [InlineData(
        """
        {
          "tag_name": "v1.3.0",
          "html_url": "http://example.com"
        }
        """)]
    public async Task FetchAsync_WhenReleaseDoesNotMatchWireFormat_ThrowsInvalidDataException(string json)
    {
        using HttpClient httpClient = new(
            new StubHttpMessageHandler(_ => CreateJsonResponse(json)));
        GitHubUpdateClient client = CreateClient(httpClient);

        await Assert.ThrowsAsync<InvalidDataException>(() => Fetch(client));
    }

    [Fact]
    public async Task FetchAsync_WhenReleaseIsNotJson_PropagatesJsonException()
    {
        using HttpClient httpClient = new(
            new StubHttpMessageHandler(_ => CreateJsonResponse("not-json")));
        GitHubUpdateClient client = CreateClient(httpClient);

        await Assert.ThrowsAnyAsync<JsonException>(() => Fetch(client));
    }

    [Fact]
    public async Task FetchAsync_WhenContentLengthExceedsLimit_ThrowsInvalidDataException()
    {
        using HttpClient httpClient = new(new StubHttpMessageHandler(_ =>
        {
            HttpResponseMessage response = CreateJsonResponse("{}");
            response.Content.Headers.ContentLength = GitHubUpdateClient.MaximumResponseSize + 1;

            return response;
        }));
        GitHubUpdateClient client = CreateClient(httpClient);

        await Assert.ThrowsAsync<InvalidDataException>(() => Fetch(client));
    }

    [Fact]
    public async Task FetchAsync_WhenStreamExceedsLimitWithoutContentLength_ThrowsInvalidDataException()
    {
        string json = new(' ', GitHubUpdateClient.MaximumResponseSize + 1);
        using HttpClient httpClient = new(new StubHttpMessageHandler(_ =>
        {
            HttpResponseMessage response = CreateJsonResponse(json);
            response.Content.Headers.ContentLength = null;

            return response;
        }));
        GitHubUpdateClient client = CreateClient(httpClient);

        await Assert.ThrowsAsync<InvalidDataException>(() => Fetch(client));
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task FetchAsync_WhenRequestIsNotSuccessful_PropagatesHttpRequestException(HttpStatusCode statusCode)
    {
        using HttpClient httpClient = new(
            new StubHttpMessageHandler(_ => new HttpResponseMessage(statusCode)));
        GitHubUpdateClient client = CreateClient(httpClient);

        await Assert.ThrowsAsync<HttpRequestException>(() => Fetch(client));
    }

    [Fact]
    public async Task FetchAsync_WhenRequestFails_PropagatesHttpRequestException()
    {
        using HttpClient httpClient = new(
            new StubHttpMessageHandler(_ => throw new HttpRequestException("Offline")));
        GitHubUpdateClient client = CreateClient(httpClient);

        await Assert.ThrowsAsync<HttpRequestException>(() => Fetch(client));
    }

    [Fact]
    public async Task FetchAsync_WhenResponseReadFails_PropagatesIOException()
    {
        using HttpClient httpClient = new(
            new StubHttpMessageHandler(_ => throw new IOException("Read failed")));
        GitHubUpdateClient client = CreateClient(httpClient);

        await Assert.ThrowsAsync<IOException>(() => Fetch(client));
    }

    [Fact]
    public async Task FetchAsync_WhenResponseOperationIsCanceled_PropagatesOperationCanceledException()
    {
        using HttpClient httpClient = new(
            new StubHttpMessageHandler(_ => throw new OperationCanceledException()));
        GitHubUpdateClient client = CreateClient(httpClient);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Fetch(client));
    }

    [Fact]
    public async Task FetchAsync_WhenCancellationIsRequestedBeforeRequest_PropagatesCancellation()
    {
        var handler = new StubHttpMessageHandler(_ => throw new InvalidOperationException());
        using HttpClient httpClient = new(handler);
        GitHubUpdateClient client = CreateClient(httpClient);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.CheckForUpdateAsync(cancellation.Token));

        Assert.Equal(0, handler.RequestCount);
    }

    private static GitHubUpdateClient CreateClient(HttpClient httpClient)
    {
        return new GitHubUpdateClient(
            httpClient);
    }

    private static string Release(string version)
    {
        return $$"""
                 {
                   "tag_name": "{{version}}",
                   "html_url": "https://example.com/releases/{{version}}",
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

    private static Task<UpdateInfo> Fetch(GitHubUpdateClient client)
    {
        return client.CheckForUpdateAsync(TestContext.Current.CancellationToken);
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public int RequestCount { get; private set; }

        public Uri? LastRequestUri { get; private set; }

        public string? LastUserAgent { get; private set; }

        public string? LastAccept { get; private set; }

        public string? LastApiVersion { get; private set; }

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
            LastApiVersion = request.Headers.GetValues("X-GitHub-Api-Version").Single();

            return Task.FromResult(_handler(request));
        }
    }
}

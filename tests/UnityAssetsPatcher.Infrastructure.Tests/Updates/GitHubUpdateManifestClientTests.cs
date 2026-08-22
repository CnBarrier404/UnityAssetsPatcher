using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using UnityAssetsPatcher.Application.Updates;
using UnityAssetsPatcher.Infrastructure.Updates;
using Xunit;

namespace UnityAssetsPatcher.Infrastructure.Tests.Updates;

public sealed class GitHubUpdateManifestClientTests
{
    private const string Sha256 = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [Fact]
    public async Task FetchAsync_WhenManifestIsValid_ReturnsManifestAndSendsExpectedRequest()
    {
        var handler = new StubHttpMessageHandler(_ => CreateJsonResponse(Manifest("v1.3.0")));
        using HttpClient httpClient = new(handler);
        GitHubUpdateManifestClient client = CreateClient(httpClient);

        UpdateInfo manifest = await Fetch(client);

        Assert.NotNull(manifest);
        Assert.Equal("v1.3.0", manifest.Version);
        Assert.Equal("https://example.com/releases/v1.3.0", manifest.ReleaseUrl.AbsoluteUri);
        Assert.Equal(
            "https://example.com/download/UnityAssetsPatcher-v1.3.0-win-x64.exe",
            manifest.DownloadUrl.AbsoluteUri);
        Assert.Equal(Sha256, manifest.Sha256);
        Assert.Equal(1, handler.RequestCount);
        Assert.Equal(GitHubUpdateManifestClient.UpdateManifestUrl, handler.LastRequestUri?.AbsoluteUri);
        Assert.Contains("UnityAssetsPatcher", handler.LastUserAgent ?? string.Empty);
        Assert.Contains("application/json", handler.LastAccept ?? string.Empty);
    }

    [Fact]
    public async Task FetchAsync_WhenSha256ContainsUppercase_NormalizesSha256ToLowercase()
    {
        using HttpClient httpClient = new(new StubHttpMessageHandler(_ =>
            CreateJsonResponse(Manifest("v1.3.0", Sha256.ToUpperInvariant()))));
        GitHubUpdateManifestClient client = CreateClient(httpClient);

        UpdateInfo manifest = await Fetch(client);

        Assert.NotNull(manifest);
        Assert.Equal(Sha256, manifest.Sha256);
    }

    [Fact]
    public async Task FetchAsync_WhenVersionIsNotSemantic_ReturnsManifestForApplicationEvaluation()
    {
        using HttpClient httpClient = new(
            new StubHttpMessageHandler(_ => CreateJsonResponse(Manifest("invalid"))));
        GitHubUpdateManifestClient client = CreateClient(httpClient);

        UpdateInfo manifest = await Fetch(client);

        Assert.NotNull(manifest);
        Assert.Equal("invalid", manifest.Version);
    }

    [Theory]
    [InlineData("{}")]
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
    public async Task FetchAsync_WhenManifestDoesNotMatchWireFormat_ThrowsInvalidDataException(string json)
    {
        using HttpClient httpClient = new(
            new StubHttpMessageHandler(_ => CreateJsonResponse(json)));
        GitHubUpdateManifestClient client = CreateClient(httpClient);

        await Assert.ThrowsAsync<InvalidDataException>(() => Fetch(client));
    }

    [Fact]
    public async Task FetchAsync_WhenManifestIsNotJson_PropagatesJsonException()
    {
        using HttpClient httpClient = new(
            new StubHttpMessageHandler(_ => CreateJsonResponse("not-json")));
        GitHubUpdateManifestClient client = CreateClient(httpClient);

        await Assert.ThrowsAnyAsync<JsonException>(() => Fetch(client));
    }

    [Fact]
    public async Task FetchAsync_WhenContentLengthExceedsLimit_ThrowsInvalidDataException()
    {
        using HttpClient httpClient = new(new StubHttpMessageHandler(_ =>
        {
            HttpResponseMessage response = CreateJsonResponse("{}");
            response.Content.Headers.ContentLength = GitHubUpdateManifestClient.MaximumManifestSize + 1;

            return response;
        }));
        GitHubUpdateManifestClient client = CreateClient(httpClient);

        await Assert.ThrowsAsync<InvalidDataException>(() => Fetch(client));
    }

    [Fact]
    public async Task FetchAsync_WhenStreamExceedsLimitWithoutContentLength_ThrowsInvalidDataException()
    {
        string json = new(' ', GitHubUpdateManifestClient.MaximumManifestSize + 1);
        using HttpClient httpClient = new(new StubHttpMessageHandler(_ =>
        {
            HttpResponseMessage response = CreateJsonResponse(json);
            response.Content.Headers.ContentLength = null;

            return response;
        }));
        GitHubUpdateManifestClient client = CreateClient(httpClient);

        await Assert.ThrowsAsync<InvalidDataException>(() => Fetch(client));
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task FetchAsync_WhenRequestIsNotSuccessful_PropagatesHttpRequestException(HttpStatusCode statusCode)
    {
        using HttpClient httpClient = new(
            new StubHttpMessageHandler(_ => new HttpResponseMessage(statusCode)));
        GitHubUpdateManifestClient client = CreateClient(httpClient);

        await Assert.ThrowsAsync<HttpRequestException>(() => Fetch(client));
    }

    [Fact]
    public async Task FetchAsync_WhenRequestFails_PropagatesHttpRequestException()
    {
        using HttpClient httpClient = new(
            new StubHttpMessageHandler(_ => throw new HttpRequestException("Offline")));
        GitHubUpdateManifestClient client = CreateClient(httpClient);

        await Assert.ThrowsAsync<HttpRequestException>(() => Fetch(client));
    }

    [Fact]
    public async Task FetchAsync_WhenResponseReadFails_PropagatesIOException()
    {
        using HttpClient httpClient = new(
            new StubHttpMessageHandler(_ => throw new IOException("Read failed")));
        GitHubUpdateManifestClient client = CreateClient(httpClient);

        await Assert.ThrowsAsync<IOException>(() => Fetch(client));
    }

    [Fact]
    public async Task FetchAsync_WhenResponseOperationIsCanceled_PropagatesOperationCanceledException()
    {
        using HttpClient httpClient = new(
            new StubHttpMessageHandler(_ => throw new OperationCanceledException()));
        GitHubUpdateManifestClient client = CreateClient(httpClient);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Fetch(client));
    }

    [Fact]
    public async Task FetchAsync_WhenCancellationIsRequestedBeforeRequest_PropagatesCancellation()
    {
        var handler = new StubHttpMessageHandler(_ => throw new InvalidOperationException());
        using HttpClient httpClient = new(handler);
        GitHubUpdateManifestClient client = CreateClient(httpClient);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.FetchAsync(cancellation.Token));

        Assert.Equal(0, handler.RequestCount);
    }

    private static GitHubUpdateManifestClient CreateClient(HttpClient httpClient)
    {
        return new GitHubUpdateManifestClient(
            httpClient,
            NullLogger<GitHubUpdateManifestClient>.Instance);
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

    private static Task<UpdateInfo> Fetch(GitHubUpdateManifestClient client)
    {
        return client.FetchAsync(TestContext.Current.CancellationToken);
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

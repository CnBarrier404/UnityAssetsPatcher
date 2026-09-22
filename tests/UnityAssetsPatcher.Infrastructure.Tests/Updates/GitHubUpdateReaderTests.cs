using System.Text;
using UnityAssetsPatcher.Infrastructure.Updates;
using Xunit;

namespace UnityAssetsPatcher.Infrastructure.Tests.Updates;

public sealed class GitHubUpdateReaderTests
{
    [Fact]
    public async Task ReadAsync_WhenReleaseIsValid_ReadsReleaseFromStream()
    {
        using var content = new MemoryStream(Encoding.UTF8.GetBytes(
            $$"""
              {
                "tag_name": "v1.3.0",
                "html_url": "https://example.com/releases/v1.3.0"
              }
              """));

        GitHubUpdateReadResult result = await GitHubUpdateReader.ReadAsync(
            content,
            GitHubUpdateClient.MaximumResponseSize,
            TestContext.Current.CancellationToken);

        Assert.Equal(GitHubUpdateReadStatus.Success, result.Status);
        Assert.NotNull(result.Release);
        Assert.Equal("v1.3.0", result.Release!.Version);
        Assert.Equal("https://example.com/releases/v1.3.0", result.Release.ReleaseUrl.AbsoluteUri);
    }

    [Fact]
    public async Task ReadAsync_WhenStreamExceedsLimit_ReturnsTooLargeStatus()
    {
        using var content = new MemoryStream(
            new byte[GitHubUpdateClient.MaximumResponseSize + 1]);

        GitHubUpdateReadResult result = await GitHubUpdateReader.ReadAsync(
            content,
            GitHubUpdateClient.MaximumResponseSize,
            TestContext.Current.CancellationToken);

        Assert.Equal(GitHubUpdateReadStatus.TooLarge, result.Status);
        Assert.Null(result.Release);
    }

    [Fact]
    public async Task ReadAsync_WhenReleaseDoesNotMatchSchema_ReturnsInvalidStatus()
    {
        using var content = new MemoryStream(Encoding.UTF8.GetBytes("{}"));

        GitHubUpdateReadResult result = await GitHubUpdateReader.ReadAsync(
            content,
            GitHubUpdateClient.MaximumResponseSize,
            TestContext.Current.CancellationToken);

        Assert.Equal(GitHubUpdateReadStatus.Invalid, result.Status);
        Assert.Null(result.Release);
    }
}

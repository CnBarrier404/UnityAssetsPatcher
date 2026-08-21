using System.Text;
using UnityAssetsPatcher.Infrastructure.Updates;
using Xunit;

namespace UnityAssetsPatcher.Infrastructure.Tests.Updates;

public sealed class UpdateManifestReaderTests
{
    private const string Sha256 = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [Fact]
    public async Task ReadAsync_WhenManifestIsValid_ReadsManifestFromStream()
    {
        using var content = new MemoryStream(Encoding.UTF8.GetBytes(
            $$"""
            {
              "schemaVersion": 1,
              "version": "v1.3.0",
              "releaseUrl": "https://example.com/releases/v1.3.0",
              "downloadUrl": "https://example.com/download/file.exe",
              "sha256": "{{Sha256}}"
            }
            """));

        UpdateManifestReadResult result = await UpdateManifestReader.ReadAsync(
            content,
            GitHubUpdateManifestClient.MaximumManifestSize,
            TestContext.Current.CancellationToken);

        Assert.Equal(UpdateManifestReadStatus.Success, result.Status);
        Assert.NotNull(result.Manifest);
        Assert.Equal("v1.3.0", result.Manifest!.Version);
        Assert.Equal("https://example.com/releases/v1.3.0", result.Manifest.ReleaseUrl.AbsoluteUri);
        Assert.Equal(Sha256, result.Manifest.Sha256);
    }

    [Fact]
    public async Task ReadAsync_WhenStreamExceedsLimit_ReturnsTooLargeStatus()
    {
        using var content = new MemoryStream(
            new byte[GitHubUpdateManifestClient.MaximumManifestSize + 1]);

        UpdateManifestReadResult result = await UpdateManifestReader.ReadAsync(
            content,
            GitHubUpdateManifestClient.MaximumManifestSize,
            TestContext.Current.CancellationToken);

        Assert.Equal(UpdateManifestReadStatus.TooLarge, result.Status);
        Assert.Null(result.Manifest);
    }

    [Fact]
    public async Task ReadAsync_WhenManifestDoesNotMatchSchema_ReturnsInvalidStatus()
    {
        using var content = new MemoryStream(Encoding.UTF8.GetBytes("{}"));

        UpdateManifestReadResult result = await UpdateManifestReader.ReadAsync(
            content,
            GitHubUpdateManifestClient.MaximumManifestSize,
            TestContext.Current.CancellationToken);

        Assert.Equal(UpdateManifestReadStatus.Invalid, result.Status);
        Assert.Null(result.Manifest);
    }
}

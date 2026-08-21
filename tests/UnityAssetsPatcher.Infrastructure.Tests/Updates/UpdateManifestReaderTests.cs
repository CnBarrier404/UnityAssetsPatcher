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

        UpdateManifest? manifest = await UpdateManifestReader.ReadAsync(
            content,
            GitHubUpdateManifestClient.MaximumManifestSize,
            TestContext.Current.CancellationToken);

        Assert.NotNull(manifest);
        Assert.Equal("v1.3.0", manifest!.Version);
        Assert.Equal("https://example.com/releases/v1.3.0", manifest.ReleaseUrl.AbsoluteUri);
        Assert.Equal(Sha256, manifest.Sha256);
    }

    [Fact]
    public async Task ReadAsync_WhenStreamExceedsLimit_ReturnsNull()
    {
        using var content = new MemoryStream(
            new byte[GitHubUpdateManifestClient.MaximumManifestSize + 1]);

        UpdateManifest? manifest = await UpdateManifestReader.ReadAsync(
            content,
            GitHubUpdateManifestClient.MaximumManifestSize,
            TestContext.Current.CancellationToken);

        Assert.Null(manifest);
    }
}

using UnityAssetsPatcher.Application.Features.Check;
using UnityAssetsPatcher.Application.Manifests;
using Xunit;

namespace UnityAssetsPatcher.Application.Tests.Features.Check;

public sealed class CheckManifestHandlerTests
{
    [Fact]
    public async Task HandleAsync_WhenManifestServiceSucceeds_ReturnsCheckResult()
    {
        ModManifest manifest = CreateManifest();
        var service = new StubManifestService((_, _) => Task.FromResult(manifest));
        var handler = new CheckManifestHandler(service);

        CheckManifestResult result = await handler.HandleAsync(
            new CheckManifestRequest("manifest.json"),
            TestContext.Current.CancellationToken);

        Assert.Same(manifest, result.Manifest);
    }

    [Fact]
    public async Task HandleAsync_WhenCalled_ForwardsSourcePathAndCancellationToken()
    {
        var service = new StubManifestService((_, _) => Task.FromResult(CreateManifest()));
        var handler = new CheckManifestHandler(service);
        using CancellationTokenSource cancellation = new();

        _ = await handler.HandleAsync(
            new CheckManifestRequest("manifest.json"),
            cancellation.Token);

        Assert.Equal("manifest.json", service.SourcePath);
        Assert.Equal(cancellation.Token, service.CancellationToken);
    }

    [Fact]
    public async Task HandleAsync_WhenManifestServiceFails_PropagatesFailure()
    {
        var expected = new ManifestException(ManifestErrorCodes.InvalidJson.Value);
        var service = new StubManifestService((_, _) => Task.FromException<ModManifest>(expected));
        var handler = new CheckManifestHandler(service);

        ManifestException exception = await Assert.ThrowsAsync<ManifestException>(() =>
            handler.HandleAsync(
                new CheckManifestRequest("manifest.json"),
                TestContext.Current.CancellationToken));

        Assert.Same(expected, exception);
    }

    private static ModManifest CreateManifest()
    {
        return new ModManifest(
            "https://uap.cnbarrier.com/schema-v1.json",
            "Test Mod",
            "Test Author",
            "1.0.0",
            null,
            null,
            [],
            [],
            []);
    }

    private sealed class StubManifestService : IModManifestService
    {
        private readonly Func<string, CancellationToken, Task<ModManifest>> _readManifest;

        public string? SourcePath { get; private set; }

        public CancellationToken CancellationToken { get; private set; }

        public StubManifestService(Func<string, CancellationToken, Task<ModManifest>> readManifest)
        {
            _readManifest = readManifest;
        }

        public Task<ModManifest> ReadManifestAsync(
            string sourcePath,
            CancellationToken cancellationToken = default)
        {
            SourcePath = sourcePath;
            CancellationToken = cancellationToken;

            return _readManifest(sourcePath, cancellationToken);
        }
    }
}

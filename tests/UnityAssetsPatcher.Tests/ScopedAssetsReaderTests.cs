using UnityAssetsPatcher.AssetsTools;
using UnityAssetsPatcher.Core.Assets;
using Xunit;

namespace UnityAssetsPatcher.Tests;

public sealed class ScopedAssetsReaderTests
{
    /// <summary>
    /// Verifies that one scoped reader reuses the same opened assets file for repeated reads.
    /// </summary>
    [Fact]
    public void ReadAssetsInfoAndFieldInfo_WhenPathRepeats_OpensAssetsFileOnce()
    {
        var factory = new RecordingAssetsFileSessionFactory();
        using var reader = new ScopedAssetsReader(factory);
        string assetsFilePath = Path.Combine(Path.GetTempPath(), "Game", "sharedassets0.assets");

        _ = reader.ReadAssetsInfo(assetsFilePath);
        _ = reader.ReadAssetsFieldInfo(assetsFilePath, 1);
        _ = reader.ReadAssetsFieldInfo(assetsFilePath.ToUpperInvariant(), 1);

        Assert.Equal(1, factory.OpenCount);
    }

    /// <summary>
    /// Verifies that cached field trees are keyed by Path ID, not only by assets file scope.
    /// </summary>
    [Fact]
    public void ReadAssetsFieldInfo_WhenPathIdsDiffer_ReturnsEachPathIdFieldTree()
    {
        var factory = new RecordingAssetsFileSessionFactory();
        using var reader = new ScopedAssetsReader(factory);
        string assetsFilePath = Path.Combine(Path.GetTempPath(), "Game", "sharedassets0.assets");

        AssetsFieldInfo first = reader.ReadAssetsFieldInfo(assetsFilePath, 1);
        AssetsFieldInfo second = reader.ReadAssetsFieldInfo(assetsFilePath, 2);

        Assert.Equal("Asset 1", first.Value);
        Assert.Equal("Asset 2", second.Value);
    }

    /// <summary>
    /// Verifies that field tree caching reuses the same Path ID entry for paths with different casing.
    /// </summary>
    [Fact]
    public void ReadAssetsFieldInfo_WhenPathCasingDiffers_ReusesCachedPathIdFieldTree()
    {
        var factory = new RecordingAssetsFileSessionFactory();
        using var reader = new ScopedAssetsReader(factory);
        string assetsFilePath = Path.Combine(Path.GetTempPath(), "Game", "sharedassets0.assets");

        _ = reader.ReadAssetsFieldInfo(assetsFilePath, 1);
        _ = reader.ReadAssetsFieldInfo(assetsFilePath.ToUpperInvariant(), 1);

        Assert.Equal(1, factory.ReadFieldInfoCount);
    }

    private sealed class RecordingAssetsFileSessionFactory : IAssetsFileSessionFactory
    {
        public int OpenCount { get; private set; }
        public int ReadFieldInfoCount { get; private set; }

        public IAssetsFileReadSession Open(string assetsFilePath)
        {
            OpenCount++;
            return new RecordingAssetsFileReadSession(this);
        }

        public void RecordReadFieldInfo()
        {
            ReadFieldInfoCount++;
        }
    }

    private sealed class RecordingAssetsFileReadSession : IAssetsFileReadSession
    {
        private readonly RecordingAssetsFileSessionFactory _factory;

        public RecordingAssetsFileReadSession(RecordingAssetsFileSessionFactory factory)
        {
            _factory = factory;
        }

        public IReadOnlyList<AssetsInfo> ReadAssetsInfo()
        {
            return [new AssetsInfo(1, 20, "Camera", 128)];
        }

        public AssetsFieldInfo ReadAssetsFieldInfo(long pathId)
        {
            _factory.RecordReadFieldInfo();
            return new AssetsFieldInfo("Camera", "Camera", $"Asset {pathId}", []);
        }

        public void Dispose() { }
    }
}

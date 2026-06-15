using UnityAssetsPatcher.AssetsTools;
using UnityAssetsPatcher.Core.Assets;
using Xunit;

namespace UnityAssetsPatcher.Tests.AssetsTools;

public sealed class AssetsToolsAccessScopeTests
{
    [Fact]
    public void ReleaseReadResources_WhenCalledMultipleTimes_DisposesReaderOnce()
    {
        var reader = new DisposableAssetsReader();
        var writer = new DisposableAssetsWriter();
        using var scope = new AssetsToolsAccessScope(reader, writer);

        scope.ReleaseReadResources();
        scope.ReleaseReadResources();

        Assert.Equal(1, reader.DisposeCount);
        Assert.Equal(0, writer.DisposeCount);
    }

    [Fact]
    public void Dispose_ReleasesReaderAndWriter()
    {
        var reader = new DisposableAssetsReader();
        var writer = new DisposableAssetsWriter();
        var scope = new AssetsToolsAccessScope(reader, writer);

        scope.Dispose();

        Assert.Equal(1, reader.DisposeCount);
        Assert.Equal(1, writer.DisposeCount);
    }

    private sealed class DisposableAssetsReader : IAssetsFileReader, IDisposable
    {
        public int DisposeCount { get; private set; }

        public IReadOnlyList<AssetsInfo> ReadAssetsInfo(string assetsFilePath)
        {
            throw new NotSupportedException();
        }

        public AssetsFieldInfo ReadAssetsFieldInfo(string assetsFilePath, long pathId)
        {
            throw new NotSupportedException();
        }

        public void Dispose()
        {
            DisposeCount++;
        }
    }

    private sealed class DisposableAssetsWriter : IAssetsFileWriter, IDisposable
    {
        public int DisposeCount { get; private set; }

        public void WritePatch(string inputPath, string outputPath, IReadOnlyList<AssetFieldPatch> plan)
        {
            throw new NotSupportedException();
        }

        public void WriteReplacements(string inputPath, string outputPath, IReadOnlyList<AssetReplacement> plan)
        {
            throw new NotSupportedException();
        }

        public void Dispose()
        {
            DisposeCount++;
        }
    }
}

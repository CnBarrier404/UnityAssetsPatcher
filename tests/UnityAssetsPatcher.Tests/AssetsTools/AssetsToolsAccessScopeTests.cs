using UnityAssetsPatcher.Application.Assets;
using UnityAssetsPatcher.AssetsTools;
using Xunit;

namespace UnityAssetsPatcher.Tests.AssetsTools;

public sealed class AssetsToolsAccessScopeTests
{
    [Fact]
    public void CloseReadSessions_WhenCalled_ForwardsToReader()
    {
        var events = new List<string>();
        var reader = new RecordingAssetsFileReader(events);
        var writer = new RecordingAssetsFileWriter(events);
        using var scope = new AssetsToolsAccessScope(reader, writer);

        scope.CloseReadSessions();
        scope.CloseReadSessions();

        Assert.Equal(2, reader.CloseReadSessionsCount);
        Assert.Equal(0, reader.DisposeCount);
        Assert.Equal(0, writer.DisposeCount);
    }

    [Fact]
    public void Dispose_WhenCalledMultipleTimes_DisposesReaderThenWriterOnce()
    {
        var events = new List<string>();
        var reader = new RecordingAssetsFileReader(events);
        var writer = new RecordingAssetsFileWriter(events);
        var scope = new AssetsToolsAccessScope(reader, writer);

        scope.Dispose();
        scope.Dispose();

        Assert.Equal(1, reader.DisposeCount);
        Assert.Equal(1, writer.DisposeCount);
        Assert.Equal(["reader", "writer"], events);
    }

    [Fact]
    public void Dispose_WhenReaderThrows_StillDisposesWriterAndRemainsIdempotent()
    {
        var events = new List<string>();
        var reader = new RecordingAssetsFileReader(events, throwOnDispose: true);
        var writer = new RecordingAssetsFileWriter(events);
        var scope = new AssetsToolsAccessScope(reader, writer);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(scope.Dispose);
        scope.Dispose();

        Assert.Equal("Test reader dispose failure.", exception.Message);
        Assert.Equal(1, reader.DisposeCount);
        Assert.Equal(1, writer.DisposeCount);
        Assert.Equal(["reader", "writer"], events);
    }

    private sealed class RecordingAssetsFileReader : IAssetsFileReader
    {
        public int CloseReadSessionsCount { get; private set; }
        public int DisposeCount { get; private set; }

        private readonly ICollection<string> _events;
        private readonly bool _throwOnDispose;

        public RecordingAssetsFileReader(ICollection<string> events, bool throwOnDispose = false)
        {
            _events = events;
            _throwOnDispose = throwOnDispose;
        }

        public IReadOnlyList<AssetInfo> ReadAssets(string assetsFilePath)
        {
            throw new NotSupportedException();
        }

        public AssetField ReadField(string assetsFilePath, long pathId)
        {
            throw new NotSupportedException();
        }

        public void CloseReadSessions()
        {
            CloseReadSessionsCount++;
        }

        public void Dispose()
        {
            DisposeCount++;
            _events.Add("reader");

            if (_throwOnDispose)
            {
                throw new InvalidOperationException("Test reader dispose failure.");
            }
        }
    }

    private sealed class RecordingAssetsFileWriter : IAssetsFileWriter
    {
        public int DisposeCount { get; private set; }

        private readonly ICollection<string> _events;

        public RecordingAssetsFileWriter(ICollection<string> events)
        {
            _events = events;
        }

        public void WriteFieldPatches(string inputPath, string outputPath, IReadOnlyList<AssetFieldPatch> plan)
        {
            throw new NotSupportedException();
        }

        public void WriteReplacements(
            string inputPath,
            string outputPath,
            IReadOnlyList<AssetReplacement> plan)
        {
            throw new NotSupportedException();
        }

        public void WriteFieldPatchesAndCopies(
            string inputPath,
            string outputPath,
            IReadOnlyList<AssetFieldPatch> fieldPatches,
            IReadOnlyList<AssetCopy> copies)
        {
            throw new NotSupportedException();
        }

        public void Dispose()
        {
            DisposeCount++;
            _events.Add("writer");
        }
    }
}

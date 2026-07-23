using UnityAssetsPatcher.Abstractions.Assets;
using UnityAssetsPatcher.AssetsTools;
using UnityAssetsPatcher.Domain.Assets;
using Xunit;

namespace UnityAssetsPatcher.Tests.AssetsTools;

public sealed class AssetsToolsAccessScopeTests
{
    [Fact]
    public void CloseReadSessions_WhenReaderWasCreated_DisposesAndRemovesReader()
    {
        var events = new List<string>();
        var readers = new List<RecordingAssetsFileReader>();
        var writer = new RecordingAssetsFileWriter(events);
        using var scope = new AssetsToolsAccessScope(
            () => CreateReader(readers, events),
            () => writer);
        IAssetsFileReader firstReader = scope.Reader;

        scope.CloseReadSessions();
        scope.CloseReadSessions();
        IAssetsFileReader secondReader = scope.Reader;

        Assert.NotSame(firstReader, secondReader);
        Assert.Equal(2, readers.Count);
        Assert.Equal(1, readers[0].DisposeCount);
        Assert.Equal(0, readers[1].DisposeCount);
        Assert.Equal(0, writer.DisposeCount);
    }

    [Fact]
    public void Dispose_WhenCalledMultipleTimes_DisposesCreatedReaderThenWriterOnce()
    {
        var events = new List<string>();
        var reader = new RecordingAssetsFileReader(events);
        var writer = new RecordingAssetsFileWriter(events);
        var scope = new AssetsToolsAccessScope(() => reader, () => writer);
        _ = scope.Reader;
        _ = scope.Writer;

        scope.Dispose();
        scope.Dispose();

        Assert.Equal(1, reader.DisposeCount);
        Assert.Equal(1, writer.DisposeCount);
        Assert.Equal(["reader", "writer"], events);
    }

    [Fact]
    public void Dispose_WhenResourcesWereNotRequested_DoesNotCreateThem()
    {
        int readerCreateCount = 0;
        int writerCreateCount = 0;
        var scope = new AssetsToolsAccessScope(
            () =>
            {
                readerCreateCount++;

                return new RecordingAssetsFileReader([]);
            },
            () =>
            {
                writerCreateCount++;

                return new RecordingAssetsFileWriter([]);
            });

        scope.Dispose();

        Assert.Equal(0, readerCreateCount);
        Assert.Equal(0, writerCreateCount);
    }

    [Fact]
    public void Dispose_WhenReaderThrows_StillDisposesWriterAndRemainsIdempotent()
    {
        var events = new List<string>();
        var reader = new RecordingAssetsFileReader(events, throwOnDispose: true);
        var writer = new RecordingAssetsFileWriter(events);
        var scope = new AssetsToolsAccessScope(() => reader, () => writer);
        _ = scope.Reader;
        _ = scope.Writer;

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(scope.Dispose);
        scope.Dispose();

        Assert.Equal("Test reader dispose failure.", exception.Message);
        Assert.Equal(1, reader.DisposeCount);
        Assert.Equal(1, writer.DisposeCount);
        Assert.Equal(["reader", "writer"], events);
    }

    private static RecordingAssetsFileReader CreateReader(
        ICollection<RecordingAssetsFileReader> readers,
        ICollection<string> events)
    {
        var reader = new RecordingAssetsFileReader(events);
        readers.Add(reader);

        return reader;
    }

    private sealed class RecordingAssetsFileReader : IAssetsFileReader
    {
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

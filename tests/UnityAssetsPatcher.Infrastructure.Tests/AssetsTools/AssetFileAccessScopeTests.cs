using System.Text.Json;
using UnityAssetsPatcher.Application.Assets;
using UnityAssetsPatcher.Domain.Assets;
using UnityAssetsPatcher.Infrastructure.AssetsTools;
using Xunit;

namespace UnityAssetsPatcher.Infrastructure.Tests.AssetsTools;

public sealed class AssetFileAccessScopeTests
{
    [Fact]
    public void ReadField_WhenSameAssetIsReadRepeatedly_UsesScopeCache()
    {
        var session = new RecordingAssetFileSession(CreateAssetField());
        var factory = new RecordingAssetFileSessionFactory(session);
        using IAssetsAccessScope scope = new AssetFileAccessScopeFactory(factory).CreateScope();

        AssetField first = scope.Reader.ReadField("input.assets", 4);
        AssetField second = scope.Reader.ReadField("input.assets", 4);

        Assert.Same(first, second);
        Assert.Equal(1, factory.OpenCount);
        Assert.Equal(1, session.ReadFieldCount);
    }

    [Fact]
    public void ReadField_WhenAnotherAssetIsRead_EvictsPreviousFieldTree()
    {
        var session = new RecordingAssetFileSession(CreateAssetField());
        var factory = new RecordingAssetFileSessionFactory(session);
        using IAssetsAccessScope scope = new AssetFileAccessScopeFactory(factory).CreateScope();

        _ = scope.Reader.ReadField("input.assets", 4);
        _ = scope.Reader.ReadField("input.assets", 5);
        _ = scope.Reader.ReadField("input.assets", 4);

        Assert.Equal(1, factory.OpenCount);
        Assert.Equal(3, session.ReadFieldCount);
    }

    [Fact]
    public void WriteFieldPatches_WhenWriteStarts_ClearsReadFieldCache()
    {
        var session = new RecordingAssetFileSession(CreateAssetField());
        var factory = new RecordingAssetFileSessionFactory(session);
        using IAssetsAccessScope scope = new AssetFileAccessScopeFactory(factory).CreateScope();

        _ = scope.Reader.ReadField("input.assets", 4);
        scope.Writer.WriteFieldPatches("input.assets", "output.assets", CreateFieldPatch());
        _ = scope.Reader.ReadField("input.assets", 4);

        Assert.Equal(3, session.ReadFieldCount);
    }

    [Fact]
    public void WriteFieldPatches_WhenAssetHasMultipleOperations_ReadsItsFieldTreeOnce()
    {
        var session = new RecordingAssetFileSession(CreateAssetField());
        var factory = new RecordingAssetFileSessionFactory(session);
        var scopeFactory = new AssetFileAccessScopeFactory(factory);
        using IAssetsAccessScope scope = scopeFactory.CreateScope();

        scope.Writer.WriteFieldPatches(
            "input.assets",
            "output.assets",
            [
                new AssetFieldPatch(
                    4,
                    [
                        new FieldPatchOperation("first", JsonValue("2")),
                        new FieldPatchOperation("second", JsonValue("3"))
                    ])
            ]);

        Assert.Equal(1, factory.OpenCount);
        Assert.Equal(1, session.ReadFieldCount);
        var patch = Assert.IsType<PatchAssetFields>(Assert.Single(session.Plan!.Mutations));
        Assert.Collection(
            patch.Assignments,
            first => Assert.Equal("first", first.Path.ToString()),
            second => Assert.Equal("second", second.Path.ToString()));
    }

    [Fact]
    public void WriteFieldPatches_WhenAssetsRepeatNonConsecutively_ReadsEachPatchFieldTree()
    {
        var session = new RecordingAssetFileSession(CreateAssetField());
        var factory = new RecordingAssetFileSessionFactory(session);
        using IAssetsAccessScope scope = new AssetFileAccessScopeFactory(factory).CreateScope();

        scope.Writer.WriteFieldPatches(
            "input.assets",
            "output.assets",
            [
                new AssetFieldPatch(4, [new FieldPatchOperation("first", JsonValue("2"))]),
                new AssetFieldPatch(5, [new FieldPatchOperation("first", JsonValue("3"))]),
                new AssetFieldPatch(4, [new FieldPatchOperation("second", JsonValue("4"))])
            ]);

        Assert.Equal(3, session.ReadFieldCount);
    }

    [Fact]
    public void WriteFieldPatches_WhenNullIsWrittenToStringField_ThrowsInsteadOfWritingEmptyString()
    {
        var session = new RecordingAssetFileSession(CreateStringAssetField());
        using IAssetsAccessScope scope = new AssetFileAccessScopeFactory(
            new RecordingAssetFileSessionFactory(session)).CreateScope();

        Assert.Throws<InvalidOperationException>(() => scope.Writer.WriteFieldPatches(
            "input.assets",
            "output.assets",
            [
                new AssetFieldPatch(
                    4,
                    [new FieldPatchOperation("name", JsonValue("null"))])
            ]));

        Assert.Null(session.Plan);
    }

    [Fact]
    public void Dispose_WhenMultipleSessionsFail_ContinuesDisposingAndCanRetry()
    {
        var firstSession = new RecordingAssetFileSession(CreateAssetField());
        firstSession.DisposeFailures.Enqueue(new InvalidOperationException("first cleanup failure"));
        var secondSession = new RecordingAssetFileSession(CreateAssetField());
        secondSession.DisposeFailures.Enqueue(new InvalidOperationException("second cleanup failure"));
        var factory = new RecordingAssetFileSessionFactory(path =>
            path.EndsWith("first.assets", StringComparison.OrdinalIgnoreCase)
                ? firstSession
                : secondSession);
        IAssetsAccessScope scope = new AssetFileAccessScopeFactory(factory).CreateScope();

        _ = scope.Reader.ReadAssets("first.assets");
        _ = scope.Reader.ReadAssets("second.assets");

        var exception = Assert.Throws<AggregateException>(() => scope.Dispose());

        Assert.Equal(2, exception.InnerExceptions.Count);
        Assert.Equal(1, firstSession.DisposeCount);
        Assert.Equal(1, secondSession.DisposeCount);

        scope.Dispose();

        Assert.Equal(2, firstSession.DisposeCount);
        Assert.Equal(2, secondSession.DisposeCount);
    }

    [Fact]
    public void WriteFieldPatches_WhenWriteAndDisposeFail_PreservesWriteExceptionAndRetriesCleanup()
    {
        var session = new RecordingAssetFileSession(CreateAssetField())
        {
            WriteException = new InvalidOperationException("write failure")
        };
        var cleanupException = new InvalidOperationException("cleanup failure");
        session.DisposeFailures.Enqueue(cleanupException);
        IAssetsAccessScope scope = new AssetFileAccessScopeFactory(
            new RecordingAssetFileSessionFactory(session)).CreateScope();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            scope.Writer.WriteFieldPatches("input.assets", "output.assets", CreateFieldPatch()));

        Assert.Same(session.WriteException, exception);
        var attachedCleanup = Assert.IsType<AggregateException>(
            exception.Data[ResourceCleanup.CleanupExceptionsDataKey]);
        Assert.Contains(cleanupException, attachedCleanup.InnerExceptions);
        Assert.Equal(1, session.DisposeCount);

        scope.Dispose();

        Assert.Equal(2, session.DisposeCount);
    }

    private static AssetField CreateAssetField()
    {
        return new AssetObjectField(
            "Root",
            "Root",
            [
                new AssetScalarField("first", "int", new AssetScalarValue.Int32(1)),
                new AssetScalarField("second", "int", new AssetScalarValue.Int32(1))
            ]);
    }

    private static AssetField CreateStringAssetField()
    {
        return new AssetObjectField(
            "Root",
            "Root",
            [new AssetScalarField("name", "string", new AssetScalarValue.String("Text"))]);
    }

    private static IReadOnlyList<AssetFieldPatch> CreateFieldPatch()
    {
        return
        [
            new AssetFieldPatch(
                4,
                [new FieldPatchOperation("first", JsonValue("2"))])
        ];
    }

    private static JsonElement JsonValue(string value)
    {
        using JsonDocument document = JsonDocument.Parse(value);

        return document.RootElement.Clone();
    }

    private sealed class RecordingAssetFileSessionFactory : IAssetFileSessionFactory
    {
        private readonly Func<string, IAssetFileSession> _open;

        public int OpenCount { get; private set; }

        public RecordingAssetFileSessionFactory(IAssetFileSession session)
        {
            _open = _ => session;
        }

        public RecordingAssetFileSessionFactory(Func<string, IAssetFileSession> open)
        {
            _open = open;
        }

        public IAssetFileSession Open(string inputPath)
        {
            OpenCount++;

            return _open(inputPath);
        }
    }

    private sealed class RecordingAssetFileSession : IAssetFileSession
    {
        private readonly AssetField _field;

        public int ReadFieldCount { get; private set; }
        public AssetMutationPlan? Plan { get; private set; }
        public int DisposeCount { get; private set; }
        public Queue<Exception> DisposeFailures { get; } = [];
        public Exception? WriteException { get; init; }

        public RecordingAssetFileSession(AssetField field)
        {
            _field = field;
        }

        public IReadOnlyList<AssetInfo> ReadAssets()
        {
            return [];
        }

        public AssetField ReadField(AssetPathId pathId)
        {
            ReadFieldCount++;

            return _field;
        }

        public void Write(string outputPath, AssetMutationPlan plan)
        {
            Plan = plan;

            if (WriteException is { } exception)
            {
                throw exception;
            }
        }

        public void Dispose()
        {
            DisposeCount++;

            if (DisposeFailures.Count > 0)
            {
                throw DisposeFailures.Dequeue();
            }
        }
    }
}

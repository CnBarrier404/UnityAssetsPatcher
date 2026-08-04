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
                        new FieldPatchOperation("second", JsonValue("3")),
                    ]),
            ]);

        Assert.Equal(1, factory.OpenCount);
        Assert.Equal(1, session.ReadFieldCount);
        PatchAssetFields patch = Assert.IsType<PatchAssetFields>(Assert.Single(session.Plan!.Mutations));
        Assert.Collection(
            patch.Assignments,
            first => Assert.Equal("first", first.Path.ToString()),
            second => Assert.Equal("second", second.Path.ToString()));
    }

    private static AssetField CreateAssetField()
    {
        return new AssetObjectField(
            "Root",
            "Root",
            [
                new AssetScalarField("first", "int", new AssetScalarValue.Int32(1)),
                new AssetScalarField("second", "int", new AssetScalarValue.Int32(1)),
            ]);
    }

    private static IReadOnlyList<AssetFieldPatch> CreateFieldPatch()
    {
        return
        [
            new AssetFieldPatch(
                4,
                [new FieldPatchOperation("first", JsonValue("2"))]),
        ];
    }

    private static JsonElement JsonValue(string value)
    {
        using JsonDocument document = JsonDocument.Parse(value);

        return document.RootElement.Clone();
    }

    private sealed class RecordingAssetFileSessionFactory : IAssetFileSessionFactory
    {
        private readonly IAssetFileSession _session;

        public int OpenCount { get; private set; }

        public RecordingAssetFileSessionFactory(IAssetFileSession session)
        {
            _session = session;
        }

        public IAssetFileSession Open(string inputPath)
        {
            OpenCount++;

            return _session;
        }
    }

    private sealed class RecordingAssetFileSession : IAssetFileSession
    {
        private readonly AssetField _field;

        public int ReadFieldCount { get; private set; }
        public AssetMutationPlan? Plan { get; private set; }

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
        }

        public void Dispose() { }
    }
}

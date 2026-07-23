using UnityAssetsPatcher.Application.Assets;
using UnityAssetsPatcher.Domain.Assets;
using Xunit;

namespace UnityAssetsPatcher.Tests.Application.Assets;

public sealed class AssetFieldNavigatorTests
{
    [Fact]
    public void FindChild_WhenMultipleChildrenShareName_ReturnsFirstMatchingChild()
    {
        AssetField first = CreateField("data", "pair", value: new AssetFieldValue.String("first"));
        AssetField second = CreateField("data", "pair", value: new AssetFieldValue.String("second"));
        AssetField parent = CreateField("Array", "Array", [first, second]);

        AssetField? result = parent.FindChild("data");

        Assert.Same(first, result);
    }

    [Fact]
    public void FindChildren_WhenMultipleChildrenShareName_ReturnsAllMatchesInOrder()
    {
        AssetField first = CreateField("data", "pair", value: new AssetFieldValue.String("first"));
        AssetField unrelated = CreateField("size", "int", value: new AssetFieldValue.Int64(2));
        AssetField second = CreateField("data", "pair", value: new AssetFieldValue.String("second"));
        AssetField parent = CreateField("Array", "Array", [first, unrelated, second]);

        IReadOnlyList<AssetField> result = parent.FindChildren("data");

        Assert.Equal([first, second], result);
    }

    [Fact]
    public void Find_WhenPathIsSingleSegment_ReturnsDescendantByName()
    {
        AssetField fieldTree = CreateMaterialFieldTree(8842);

        AssetField? field = AssetFieldNavigator.Find(fieldTree, "m_PathID");

        Assert.NotNull(field);
        Assert.Equal("m_PathID", field.Name);
        Assert.Equal("17", field.Value?.ToInvariantString());
    }

    [Fact]
    public void Find_WhenPathUsesChildValueSelector_ReturnsSelectedDescendant()
    {
        AssetField fieldTree = CreateMaterialFieldTree(8842);

        AssetField? field = AssetFieldNavigator.Find(
            fieldTree,
            "m_SavedProperties.m_TexEnvs.Array.data[first=_EmissionMap].second.m_Texture.m_PathID");

        Assert.NotNull(field);
        Assert.Equal("m_PathID", field.Name);
        Assert.Equal("8842", field.Value?.ToInvariantString());
    }

    [Fact]
    public void Find_WhenSelectorDoesNotMatch_ReturnsNull()
    {
        AssetField fieldTree = CreateMaterialFieldTree(8842);

        AssetField? field = AssetFieldNavigator.Find(
            fieldTree,
            "m_SavedProperties.m_TexEnvs.Array.data[first=_BumpMap].second.m_Texture.m_PathID");

        Assert.Null(field);
    }

    [Fact]
    public void Find_WhenResolverIsReused_CachesVisitedChildIndexesAndDescendantIndex()
    {
        var first = new MutableField("first");
        var second = new MutableField("second");
        var container = new MutableField("container", [first, second]);
        var target = new MutableField("target");
        var root = new MutableField("root", [container, target]);
        var enumerationCounts = new Dictionary<MutableField, int>(ReferenceEqualityComparer.Instance);
        var resolver = new AssetFieldPathResolver<MutableField>(
            root,
            field => field.Name,
            field =>
            {
                enumerationCounts[field] = enumerationCounts.GetValueOrDefault(field) + 1;

                return field.Children;
            },
            field => field.Value,
            (field, name) => field.Children.Where(child => child.Name == name));

        MutableField? firstResult = resolver.Find("container.first");
        MutableField? secondResult = resolver.Find("container.second");
        MutableField? targetResult = resolver.Find("target");
        MutableField? repeatedTargetResult = resolver.Find("target");

        Assert.Same(first, firstResult);
        Assert.Same(second, secondResult);
        Assert.Same(target, targetResult);
        Assert.Same(target, repeatedTargetResult);
        Assert.All(
            new[] { root, container, first, second, target },
            field => Assert.Equal(1, enumerationCounts[field]));
    }

    [Fact]
    public void InvalidateStructure_WhenTreeStructureChanges_RebuildsIndexes()
    {
        var root = new MutableField("root");
        var resolver = new AssetFieldPathResolver<MutableField>(
            root,
            field => field.Name,
            field => field.Children,
            field => field.Value,
            (field, name) => field.Children.Where(child => child.Name == name));
        var added = new MutableField("added");

        Assert.Null(resolver.Find("added"));

        root.Children.Add(added);

        Assert.Null(resolver.Find("added"));

        resolver.InvalidateStructure();

        Assert.Same(added, resolver.Find("added"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("root..child")]
    [InlineData("data[]")]
    [InlineData("[first=value]")]
    public void Find_WhenPathSyntaxIsInvalid_ThrowsClearError(string path)
    {
        AssetField fieldTree = CreateField("root", "Root");

        Assert.Throws<InvalidOperationException>(() => AssetFieldNavigator.Find(fieldTree, path));
    }

    [Fact]
    public void ResolveArray_WhenFieldIsArray_ReturnsField()
    {
        AssetField arrayField = CreateField("Array", "Array");

        AssetField? result = AssetFieldNavigator.ResolveArray(arrayField);

        Assert.Same(arrayField, result);
    }

    [Fact]
    public void ResolveArray_WhenDirectChildIsArray_ReturnsArrayChild()
    {
        AssetField arrayField = CreateField("Array", "Array");
        AssetField parent = CreateField("m_Component", "vector", [arrayField]);

        AssetField? result = AssetFieldNavigator.ResolveArray(parent);

        Assert.Same(arrayField, result);
    }

    [Fact]
    public void ResolveArray_WhenFieldIsNull_ReturnsNull()
    {
        AssetField? result = AssetFieldNavigator.ResolveArray(null);

        Assert.Null(result);
    }

    [Fact]
    public void GetArrayElements_WhenArrayContainsDataChildren_ReturnsDataChildren()
    {
        AssetField firstData = CreateField("data", "pair");
        AssetField secondData = CreateField("data", "pair");
        AssetField size = CreateField("size", "int", value: new AssetFieldValue.Int64(2));
        AssetField arrayField = CreateField("Array", "Array", [size, firstData, secondData]);

        IReadOnlyList<AssetField> result = AssetFieldNavigator.GetArrayElements(arrayField);

        Assert.Equal([firstData, secondData], result);
    }

    [Fact]
    public void GetArrayElements_WhenArrayHasNoDataChildren_ReturnsAllChildren()
    {
        AssetField first = CreateField("first", "int", value: new AssetFieldValue.Int64(1));
        AssetField second = CreateField("second", "int", value: new AssetFieldValue.Int64(2));
        AssetField arrayField = CreateField("Array", "Array", [first, second]);

        IReadOnlyList<AssetField> result = AssetFieldNavigator.GetArrayElements(arrayField);

        Assert.Equal([first, second], result);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GetArrayElements_WhenArrayOnlyContainsNumericSizeMetadata_ReturnsEmpty(bool unsigned)
    {
        AssetFieldValue sizeValue = unsigned
            ? new AssetFieldValue.UInt64(0)
            : new AssetFieldValue.Int64(0);
        AssetField size = CreateField("size", unsigned ? "UInt32" : "int", value: sizeValue);
        AssetField arrayField = CreateField("Array", "Array", [size]);

        IReadOnlyList<AssetField> result = AssetFieldNavigator.GetArrayElements(arrayField);

        Assert.Empty(result);
    }

    private static AssetField CreateField(
        string name,
        string typeName,
        IReadOnlyList<AssetField>? children = null,
        AssetFieldValue? value = null)
    {
        return new AssetField(name, typeName, value, children ?? []);
    }

    private static AssetField CreateMaterialFieldTree(long pathId)
    {
        return new AssetField(
            "Material",
            "Material",
            null,
            [
                new AssetField("m_SavedProperties", "UnityPropertySheet", null,
                [
                    new AssetField("m_TexEnvs", "map", null,
                    [
                        new AssetField("Array", "Array", null,
                        [
                            CreateTexEnv("_MainTex", 17),
                            CreateTexEnv("_EmissionMap", pathId),
                        ]),
                    ]),
                ]),
            ]);
    }

    private static AssetField CreateTexEnv(string name, long pathId)
    {
        return new AssetField(
            "data",
            "pair",
            null,
            [
                new AssetField("first", "string", new AssetFieldValue.String(name), []),
                new AssetField("second", "UnityTexEnv", null,
                [
                    new AssetField("m_Texture", "PPtr<Texture2D>", null,
                    [
                        new AssetField("m_FileID", "int", new AssetFieldValue.Int64(0), []),
                        new AssetField("m_PathID", "SInt64", new AssetFieldValue.Int64(pathId), []),
                    ]),
                ]),
            ]);
    }

    private sealed class MutableField
    {
        public List<MutableField> Children { get; }
        public string Name { get; }
        public string? Value { get; }

        public MutableField(string name, IEnumerable<MutableField>? children = null, string? value = null)
        {
            Name = name;
            Children = children?.ToList() ?? [];
            Value = value;
        }
    }
}

using UnityAssetsPatcher.Application.Assets;
using Xunit;

namespace UnityAssetsPatcher.Tests.Application.Assets;

public sealed class AssetFieldNavigatorTests
{
    [Fact]
    public void Child_WhenMultipleChildrenShareName_ReturnsFirstMatchingChild()
    {
        AssetsFieldInfo first = CreateField("data", "pair", value: "first");
        AssetsFieldInfo second = CreateField("data", "pair", value: "second");
        AssetsFieldInfo parent = CreateField("Array", "Array", [first, second]);

        AssetsFieldInfo? result = parent.Child("data");

        Assert.Same(first, result);
    }

    [Fact]
    public void ChildrenNamed_WhenMultipleChildrenShareName_ReturnsAllMatchesInOrder()
    {
        AssetsFieldInfo first = CreateField("data", "pair", value: "first");
        AssetsFieldInfo unrelated = CreateField("size", "int", value: "2");
        AssetsFieldInfo second = CreateField("data", "pair", value: "second");
        AssetsFieldInfo parent = CreateField("Array", "Array", [first, unrelated, second]);

        IReadOnlyList<AssetsFieldInfo> result = parent.ChildrenNamed("data");

        Assert.Equal([first, second], result);
    }

    [Fact]
    public void FindField_WhenPathIsSingleSegment_ReturnsDescendantByName()
    {
        AssetsFieldInfo fieldTree = CreateMaterialFieldTree("8842");

        AssetsFieldInfo? field = AssetFieldNavigator.FindField(fieldTree, "m_PathID");

        Assert.NotNull(field);
        Assert.Equal("m_PathID", field.Name);
        Assert.Equal("17", field.Value?.ToInvariantString());
    }

    [Fact]
    public void FindField_WhenPathUsesChildValueSelector_ReturnsSelectedDescendant()
    {
        AssetsFieldInfo fieldTree = CreateMaterialFieldTree("8842");

        AssetsFieldInfo? field = AssetFieldNavigator.FindField(
            fieldTree,
            "m_SavedProperties.m_TexEnvs.Array.data[first=_EmissionMap].second.m_Texture.m_PathID");

        Assert.NotNull(field);
        Assert.Equal("m_PathID", field.Name);
        Assert.Equal("8842", field.Value?.ToInvariantString());
    }

    [Fact]
    public void FindField_WhenSelectorDoesNotMatch_ReturnsNull()
    {
        AssetsFieldInfo fieldTree = CreateMaterialFieldTree("8842");

        AssetsFieldInfo? field = AssetFieldNavigator.FindField(
            fieldTree,
            "m_SavedProperties.m_TexEnvs.Array.data[first=_BumpMap].second.m_Texture.m_PathID");

        Assert.Null(field);
    }

    [Fact]
    public void ResolveArrayField_WhenFieldIsArray_ReturnsField()
    {
        AssetsFieldInfo arrayField = CreateField("Array", "Array");

        AssetsFieldInfo? result = AssetFieldNavigator.ResolveArrayField(arrayField);

        Assert.Same(arrayField, result);
    }

    [Fact]
    public void ResolveArrayField_WhenDirectChildIsArray_ReturnsArrayChild()
    {
        AssetsFieldInfo arrayField = CreateField("Array", "Array");
        AssetsFieldInfo parent = CreateField("m_Component", "vector", [arrayField]);

        AssetsFieldInfo? result = AssetFieldNavigator.ResolveArrayField(parent);

        Assert.Same(arrayField, result);
    }

    [Fact]
    public void ResolveArrayField_WhenFieldIsNull_ReturnsNull()
    {
        AssetsFieldInfo? result = AssetFieldNavigator.ResolveArrayField(null);

        Assert.Null(result);
    }

    [Fact]
    public void GetArrayElementFields_WhenArrayContainsDataChildren_ReturnsDataChildren()
    {
        AssetsFieldInfo firstData = CreateField("data", "pair");
        AssetsFieldInfo secondData = CreateField("data", "pair");
        AssetsFieldInfo size = CreateField("size", "int", value: "2");
        AssetsFieldInfo arrayField = CreateField("Array", "Array", [size, firstData, secondData]);

        var result = AssetFieldNavigator.GetArrayElementFields(arrayField);

        Assert.Equal([firstData, secondData], result);
    }

    [Fact]
    public void GetArrayElementFields_WhenArrayHasNoDataChildren_ReturnsAllChildren()
    {
        AssetsFieldInfo first = CreateField("first", "int", value: "1");
        AssetsFieldInfo second = CreateField("second", "int", value: "2");
        AssetsFieldInfo arrayField = CreateField("Array", "Array", [first, second]);

        var result = AssetFieldNavigator.GetArrayElementFields(arrayField);

        Assert.Equal([first, second], result);
    }

    [Theory]
    [InlineData("int")]
    [InlineData("short")]
    [InlineData("long")]
    [InlineData("byte")]
    [InlineData("UInt32")]
    [InlineData("SInt64")]
    public void GetArrayElementFields_WhenArrayOnlyContainsSizeMetadata_ReturnsEmpty(string sizeTypeName)
    {
        AssetsFieldInfo size = CreateField("size", sizeTypeName, value: "0");
        AssetsFieldInfo arrayField = CreateField("Array", "Array", [size]);

        var result = AssetFieldNavigator.GetArrayElementFields(arrayField);

        Assert.Empty(result);
    }

    private static AssetsFieldInfo CreateField(
        string name,
        string typeName,
        IReadOnlyList<AssetsFieldInfo>? children = null,
        string? value = null)
    {
        return new AssetsFieldInfo(name, typeName, value, children ?? []);
    }

    private static AssetsFieldInfo CreateMaterialFieldTree(string pathId)
    {
        return new AssetsFieldInfo(
            "Material",
            "Material",
            null,
            [
                new AssetsFieldInfo("m_SavedProperties", "UnityPropertySheet", null,
                [
                    new AssetsFieldInfo("m_TexEnvs", "map", null,
                    [
                        new AssetsFieldInfo("Array", "Array", null,
                        [
                            CreateTexEnv("_MainTex", "17"),
                            CreateTexEnv("_EmissionMap", pathId),
                        ]),
                    ]),
                ]),
            ]);
    }

    private static AssetsFieldInfo CreateTexEnv(string name, string pathId)
    {
        return new AssetsFieldInfo(
            "data",
            "pair",
            null,
            [
                new AssetsFieldInfo("first", "string", name, []),
                new AssetsFieldInfo("second", "UnityTexEnv", null,
                [
                    new AssetsFieldInfo("m_Texture", "PPtr<Texture2D>", null,
                    [
                        new AssetsFieldInfo("m_FileID", "int", "0", []),
                        new AssetsFieldInfo("m_PathID", "SInt64", pathId, []),
                    ]),
                ]),
            ]);
    }
}

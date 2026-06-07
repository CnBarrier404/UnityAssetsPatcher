using UnityAssetsPatcher.Core.Assets;
using Xunit;

namespace UnityAssetsPatcher.Tests;

public sealed class AssetFieldNavigatorTests
{
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

    private static AssetsFieldInfo CreateField(
        string name,
        string typeName,
        IReadOnlyList<AssetsFieldInfo>? children = null,
        string? value = null)
    {
        return new AssetsFieldInfo(name, typeName, value, children ?? []);
    }
}

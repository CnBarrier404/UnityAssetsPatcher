using AssetsTools.NET;
using UnityAssetsPatcher.Domain.Assets;
using UnityAssetsPatcher.Infrastructure.AssetsTools;
using Xunit;

namespace UnityAssetsPatcher.Infrastructure.Tests.AssetsTools;

public sealed class AssetFieldLocatorTests
{
    [Fact]
    public void Find_WhenPathUsesSelector_ReturnsMatchingNestedField()
    {
        AssetTypeValueField first = CreateEntry("first", 10);
        AssetTypeValueField second = CreateEntry("second", 20);
        AssetTypeValueField array = TestAssetFieldFactory.Object("Array", "Array", first, second);
        AssetTypeValueField items = TestAssetFieldFactory.Object("items", "vector", array);
        AssetTypeValueField root = TestAssetFieldFactory.Object("Base", "Test", items);
        var locator = new AssetFieldLocator(root);

        AssetTypeValueField? result = locator.Find(new AssetFieldPath("items.Array.data[id=second].value"));

        Assert.NotNull(result);
        Assert.Equal(20, result.AsInt);
    }

    [Fact]
    public void InvalidateStructure_WhenChildrenChange_RefreshesCachedLookup()
    {
        AssetTypeValueField original =
            TestAssetFieldFactory.Scalar("value", "int", new AssetTypeValue(1));
        AssetTypeValueField root = TestAssetFieldFactory.Object("Base", "Test", original);
        var locator = new AssetFieldLocator(root);
        AssetTypeValueField? initial = locator.Find(new AssetFieldPath("value"));
        AssetTypeValueField replacement =
            TestAssetFieldFactory.Scalar("value", "int", new AssetTypeValue(2));
        root.Children.Clear();
        root.Children.Add(replacement);

        AssetTypeValueField? cached = locator.Find(new AssetFieldPath("value"));
        locator.InvalidateStructure();
        AssetTypeValueField? refreshed = locator.Find(new AssetFieldPath("value"));

        Assert.Same(original, initial);
        Assert.Same(original, cached);
        Assert.Same(replacement, refreshed);
    }

    private static AssetTypeValueField CreateEntry(string id, int value)
    {
        AssetTypeValueField idField =
            TestAssetFieldFactory.Scalar("id", "string", new AssetTypeValue(id));
        AssetTypeValueField valueField =
            TestAssetFieldFactory.Scalar("value", "int", new AssetTypeValue(value));

        return TestAssetFieldFactory.Object("data", "Entry", idField, valueField);
    }
}

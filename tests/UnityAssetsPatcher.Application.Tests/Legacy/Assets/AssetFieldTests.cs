using UnityAssetsPatcher.Domain.Assets;
using Xunit;

namespace UnityAssetsPatcher.Tests.Application.Assets;

public sealed class AssetFieldTests
{
    [Fact]
    public void Constructor_WhenValueTypeDiffersFromUnityTypeName_PreservesExplicitValue()
    {
        var value = new AssetFieldValue.String("1.5");

        var field = TestAssetField.Create("value", "float", value, []);

        Assert.Equal(value, field.Value);
    }

    [Fact]
    public void Constructor_WhenChildrenCollectionChanges_PreservesOriginalTree()
    {
        var children = new List<AssetField>
        {
            TestAssetField.Create("first", "string", new AssetFieldValue.String("value"), []),
        };
        var field = TestAssetField.Create("root", "Root", null, children);

        children.Add(TestAssetField.Create("second", "string", new AssetFieldValue.String("value"), []));

        Assert.Single(field.Children);
        Assert.Equal("first", field.Children[0].Name);
    }
}

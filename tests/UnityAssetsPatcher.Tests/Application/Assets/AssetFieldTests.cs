using UnityAssetsPatcher.Application.Assets;
using Xunit;

namespace UnityAssetsPatcher.Tests.Application.Assets;

public sealed class AssetFieldTests
{
    [Fact]
    public void Constructor_WhenValueTypeDiffersFromUnityTypeName_PreservesExplicitValue()
    {
        var value = new AssetFieldValue.String("1.5");

        var field = new AssetField("value", "float", value, []);

        Assert.Same(value, field.Value);
    }

    [Fact]
    public void Constructor_WhenChildrenCollectionChanges_PreservesOriginalTree()
    {
        var children = new List<AssetField>
        {
            new("first", "string", new AssetFieldValue.String("value"), []),
        };
        var field = new AssetField("root", "Root", null, children);

        children.Add(new AssetField("second", "string", new AssetFieldValue.String("value"), []));

        Assert.Single(field.Children);
        Assert.Equal("first", field.Children[0].Name);
    }
}

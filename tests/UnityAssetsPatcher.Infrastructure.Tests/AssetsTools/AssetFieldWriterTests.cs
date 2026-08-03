using AssetsTools.NET;
using UnityAssetsPatcher.Domain.Assets;
using UnityAssetsPatcher.Infrastructure.AssetsTools;
using Xunit;

namespace UnityAssetsPatcher.Infrastructure.Tests.AssetsTools;

public sealed class AssetFieldWriterTests
{
    [Fact]
    public void Write_WhenValueIsScalar_UpdatesValueWithoutChangingStructure()
    {
        AssetTypeValueField field =
            TestAssetFieldFactory.Scalar("count", "int", new AssetTypeValue(1));
        var value = new AssetScalarWriteValue(new AssetScalarValue.Int32(42));

        bool structureChanged = AssetFieldWriter.Write(field, value);

        Assert.False(structureChanged);
        Assert.Equal(42, field.AsInt);
    }

    [Fact]
    public void Write_WhenValueIsArray_UpdatesElementsAndSize()
    {
        AssetTypeTemplateField elementTemplate =
            TestAssetFieldFactory.ScalarTemplate("data", "int", AssetValueType.Int32);
        AssetTypeValueField field = TestAssetFieldFactory.Array("numbers", "vector", elementTemplate);
        var value = new AssetScalarArrayWriteValue(
            AssetScalarKind.Int32,
            [new AssetScalarValue.Int32(3), new AssetScalarValue.Int32(5)]);

        bool structureChanged = AssetFieldWriter.Write(field, value);

        Assert.True(structureChanged);
        Assert.Equal(2, field.AsArray.size);
        Assert.Equal([3, 5], field.Children.Select(child => child.AsInt));
    }
}

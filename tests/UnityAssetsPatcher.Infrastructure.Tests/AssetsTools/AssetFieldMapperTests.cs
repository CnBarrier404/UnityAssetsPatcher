using AssetsTools.NET;
using UnityAssetsPatcher.Domain.Assets;
using UnityAssetsPatcher.Infrastructure.AssetsTools;
using Xunit;

namespace UnityAssetsPatcher.Infrastructure.Tests.AssetsTools;

public sealed class AssetFieldMapperTests
{
    [Fact]
    public void Map_WhenFieldIsScalar_MapsValueAndMetadata()
    {
        AssetTypeValueField source =
            TestAssetFieldFactory.Scalar("value", "int", new AssetTypeValue(42));

        AssetField result = AssetFieldMapper.Map(source);

        AssetScalarField scalar = Assert.IsType<AssetScalarField>(result);
        Assert.Equal("value", scalar.Name);
        Assert.Equal("int", scalar.TypeName);
        Assert.Equal(new AssetScalarValue.Int32(42), scalar.Value);
    }

    [Fact]
    public void Map_WhenFieldIsObject_MapsChildren()
    {
        AssetTypeValueField child = TestAssetFieldFactory.Scalar("enabled", "bool", new AssetTypeValue(true));
        AssetTypeValueField source = TestAssetFieldFactory.Object("settings", "Settings", child);

        AssetField result = AssetFieldMapper.Map(source);

        AssetObjectField objectField = Assert.IsType<AssetObjectField>(result);
        AssetScalarField mappedChild = Assert.IsType<AssetScalarField>(Assert.Single(objectField.Children));
        Assert.Equal(new AssetScalarValue.Boolean(true), mappedChild.Value);
    }

    [Fact]
    public void Map_WhenFieldIsArray_MapsElementsAndSchema()
    {
        AssetTypeTemplateField elementTemplate =
            TestAssetFieldFactory.ScalarTemplate("data", "int", AssetValueType.Int32);
        AssetTypeValueField first = TestAssetFieldFactory.Scalar("data", "int", new AssetTypeValue(10));
        AssetTypeValueField second = TestAssetFieldFactory.Scalar("data", "int", new AssetTypeValue(20));
        AssetTypeValueField source = TestAssetFieldFactory.Array("Array", "vector", elementTemplate, first, second);

        AssetField result = AssetFieldMapper.Map(source);

        AssetArrayField array = Assert.IsType<AssetArrayField>(result);
        var schema = Assert.IsType<AssetScalarFieldSchema>(array.ElementSchema);
        Assert.Equal(AssetScalarKind.Int32, schema.Kind);
        Assert.Equal(
            [new AssetScalarValue.Int32(10), new AssetScalarValue.Int32(20)],
            array.Elements.Cast<AssetScalarField>().Select(element => element.Value));
    }
}

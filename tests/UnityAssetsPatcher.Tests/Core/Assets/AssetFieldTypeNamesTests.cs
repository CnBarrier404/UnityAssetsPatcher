using UnityAssetsPatcher.Core.Assets;
using Xunit;

namespace UnityAssetsPatcher.Tests.Core.Assets;

public sealed class AssetFieldTypeNamesTests
{
    [Theory]
    [InlineData("string", true)]
    [InlineData("String", true)]
    [InlineData("int", false)]
    public void IsString_ReturnsWhetherTypeNameIsString(string typeName, bool expected)
    {
        Assert.Equal(expected, AssetFieldTypeNames.IsString(typeName));
    }

    [Theory]
    [InlineData("Array", true)]
    [InlineData("array", true)]
    [InlineData("string", false)]
    public void IsArray_ReturnsWhetherTypeNameIsArray(string typeName, bool expected)
    {
        Assert.Equal(expected, AssetFieldTypeNames.IsArray(typeName));
    }
}

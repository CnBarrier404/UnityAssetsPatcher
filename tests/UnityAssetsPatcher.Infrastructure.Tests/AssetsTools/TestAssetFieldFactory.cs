using AssetsTools.NET;

namespace UnityAssetsPatcher.Infrastructure.Tests.AssetsTools;

internal static class TestAssetFieldFactory
{
    public static AssetTypeValueField Scalar(string name, string type, AssetTypeValue value)
    {
        var template = new AssetTypeTemplateField
        {
            Name = name,
            Type = type,
            ValueType = value.ValueType,
            HasValue = true,
            Children = [],
        };
        var field = new AssetTypeValueField();
        field.Read(value, template, []);

        return field;
    }

    public static AssetTypeValueField Object(
        string name,
        string type,
        params AssetTypeValueField[] children)
    {
        var template = new AssetTypeTemplateField
        {
            Name = name,
            Type = type,
            ValueType = AssetValueType.None,
            HasValue = false,
            Children = [.. children.Select(child => child.TemplateField)],
        };
        var field = new AssetTypeValueField();
        field.Read(new AssetTypeValue(AssetValueType.None, null!), template, [.. children]);

        return field;
    }

    public static AssetTypeValueField Array(
        string name,
        string type,
        AssetTypeTemplateField elementTemplate,
        params AssetTypeValueField[] elements)
    {
        var template = new AssetTypeTemplateField
        {
            Name = name,
            Type = type,
            ValueType = AssetValueType.Array,
            IsArray = true,
            HasValue = true,
            Children = [elementTemplate],
        };
        var field = new AssetTypeValueField();
        field.Read(
            new AssetTypeValue(AssetValueType.Array, new AssetTypeArrayInfo(elements.Length)),
            template,
            [.. elements]);

        return field;
    }

    public static AssetTypeTemplateField ScalarTemplate(string name, string type, AssetValueType valueType)
    {
        return new AssetTypeTemplateField
        {
            Name = name,
            Type = type,
            ValueType = valueType,
            HasValue = true,
            Children = [],
        };
    }
}

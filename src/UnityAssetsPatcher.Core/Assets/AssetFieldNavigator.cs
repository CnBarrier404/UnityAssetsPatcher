namespace UnityAssetsPatcher.Core.Assets;

public static class AssetFieldNavigator
{
    public static AssetsFieldInfo? FindDirectChild(AssetsFieldInfo field, string name)
    {
        return field.Children.FirstOrDefault(child => string.Equals(child.Name, name, StringComparison.Ordinal));
    }

    public static AssetsFieldInfo? ResolveArrayField(AssetsFieldInfo? field)
    {
        if (field is null)
        {
            return null;
        }

        return IsArrayField(field)
            ? field
            : field.Children.FirstOrDefault(IsArrayField);
    }

    public static IReadOnlyList<AssetsFieldInfo> GetArrayElementFields(AssetsFieldInfo arrayField)
    {
        var dataChildren = arrayField.Children
            .Where(child => string.Equals(child.Name, "data", StringComparison.Ordinal))
            .ToArray();

        return dataChildren.Length > 0 ? dataChildren : arrayField.Children;
    }

    private static bool IsArrayField(AssetsFieldInfo field)
    {
        return string.Equals(field.Name, "Array", StringComparison.Ordinal) ||
               string.Equals(field.TypeName, "Array", StringComparison.Ordinal);
    }
}

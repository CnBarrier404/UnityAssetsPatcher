namespace UnityAssetsPatcher.Core.Assets;

public static class AssetFieldTypeNames
{
    public static bool IsString(string typeName)
    {
        return string.Equals(typeName, "string", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsArray(string typeName)
    {
        return string.Equals(typeName, "Array", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsInteger(string typeName)
    {
        return IsSignedInteger(typeName) || IsUnsignedInteger(typeName);
    }

    public static bool IsSignedInteger(string typeName)
    {
        return typeName.Equals("int", StringComparison.OrdinalIgnoreCase) ||
               typeName.Equals("short", StringComparison.OrdinalIgnoreCase) ||
               typeName.Equals("long", StringComparison.OrdinalIgnoreCase) ||
               typeName.StartsWith("int", StringComparison.OrdinalIgnoreCase) ||
               typeName.StartsWith("sint", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsUnsignedInteger(string typeName)
    {
        return typeName.Equals("byte", StringComparison.OrdinalIgnoreCase) ||
               typeName.StartsWith("uint", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsBoolean(string typeName)
    {
        return string.Equals(typeName, "bool", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(typeName, "boolean", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsFloatingPoint(string typeName)
    {
        return IsFloat(typeName) || IsDouble(typeName);
    }

    public static bool IsFloat(string typeName) =>
        string.Equals(typeName, "float", StringComparison.OrdinalIgnoreCase);

    public static bool IsDouble(string typeName) =>
        string.Equals(typeName, "double", StringComparison.OrdinalIgnoreCase);
}

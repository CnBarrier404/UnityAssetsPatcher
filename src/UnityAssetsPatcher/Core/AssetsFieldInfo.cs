namespace UnityAssetsPatcher.Core;

public sealed class AssetsFieldInfo
{
    public AssetsFieldInfo(string name, string typeName, string? value, IReadOnlyList<AssetsFieldInfo> children)
    {
        Name = name;
        TypeName = typeName;
        Value = value;
        Children = children;
    }

    public string Name { get; }
    public string TypeName { get; }
    public string? Value { get; }
    public IReadOnlyList<AssetsFieldInfo> Children { get; }
}

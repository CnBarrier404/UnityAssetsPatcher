namespace UnityAssetsPatcher.Core.Assets;

public sealed class AssetsFieldInfo
{
    public IReadOnlyList<AssetsFieldInfo> Children { get; }
    public string Name { get; }
    public string TypeName { get; }
    public string? Value { get; }

    private readonly Dictionary<string, IReadOnlyList<AssetsFieldInfo>>? _childrenByName;

    public AssetsFieldInfo(string name, string typeName, string? value, IReadOnlyList<AssetsFieldInfo> children)
    {
        Name = name;
        TypeName = typeName;
        Value = value;
        Children = children.ToArray();
        _childrenByName = BuildChildrenByName(Children);
    }

    public AssetsFieldInfo? Child(string name)
    {
        var children = ChildrenNamed(name);

        return children.Count > 0
            ? children[0]
            : null;
    }

    public IReadOnlyList<AssetsFieldInfo> ChildrenNamed(string name)
    {
        return _childrenByName is not null &&
               _childrenByName.TryGetValue(name, out var children)
            ? children
            : [];
    }

    private static Dictionary<string, IReadOnlyList<AssetsFieldInfo>>? BuildChildrenByName(
        IReadOnlyList<AssetsFieldInfo> children)
    {
        if (children.Count == 0)
        {
            return null;
        }

        var builder = new Dictionary<string, List<AssetsFieldInfo>>(children.Count, StringComparer.Ordinal);

        foreach (AssetsFieldInfo child in children)
        {
            if (!builder.TryGetValue(child.Name, out var namedChildren))
            {
                namedChildren = [];
                builder.Add(child.Name, namedChildren);
            }

            namedChildren.Add(child);
        }

        return builder.ToDictionary(
            static pair => pair.Key,
            static IReadOnlyList<AssetsFieldInfo> (pair) => pair.Value.ToArray(),
            StringComparer.Ordinal);
    }
}

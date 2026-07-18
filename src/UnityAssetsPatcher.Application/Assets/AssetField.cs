namespace UnityAssetsPatcher.Application.Assets;

public sealed class AssetField
{
    public IReadOnlyList<AssetField> Children { get; }
    public string Name { get; }
    public string TypeName { get; }
    public AssetFieldValue? Value { get; }

    private readonly Dictionary<string, IReadOnlyList<AssetField>>? _childrenByName;

    public AssetField(
        string name,
        string typeName,
        AssetFieldValue? value,
        IReadOnlyList<AssetField> children)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(typeName);
        ArgumentNullException.ThrowIfNull(children);

        Name = name;
        TypeName = typeName;
        Value = value;
        Children = children.ToArray();
        _childrenByName = BuildChildrenByName(Children);
    }

    public AssetField? FindChild(string name)
    {
        var children = FindChildren(name);

        return children.Count > 0
            ? children[0]
            : null;
    }

    public IReadOnlyList<AssetField> FindChildren(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        return _childrenByName is not null &&
               _childrenByName.TryGetValue(name, out var children)
            ? children
            : [];
    }

    private static Dictionary<string, IReadOnlyList<AssetField>>? BuildChildrenByName(
        IReadOnlyList<AssetField> children)
    {
        if (children.Count == 0)
        {
            return null;
        }

        var builder = new Dictionary<string, List<AssetField>>(children.Count, StringComparer.Ordinal);

        foreach (AssetField child in children)
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
            static IReadOnlyList<AssetField> (pair) => pair.Value.ToArray(),
            StringComparer.Ordinal);
    }
}

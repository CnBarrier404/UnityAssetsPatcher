namespace UnityAssetsPatcher.Application.Assets;

public sealed class AssetField
{
    public IReadOnlyList<AssetField> Children => _children;
    public string Name { get; }
    public string TypeName { get; }
    public AssetFieldValue? Value { get; }

    private readonly AssetField[] _children;

    public AssetField(
        string name,
        string typeName,
        AssetFieldValue? value,
        IEnumerable<AssetField> children)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(typeName);
        ArgumentNullException.ThrowIfNull(children);

        Name = name;
        TypeName = typeName;
        Value = value;
        _children = children.ToArray();
    }

    public AssetField? FindChild(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        return _children.FirstOrDefault(child => string.Equals(child.Name, name, StringComparison.Ordinal));
    }

    public IReadOnlyList<AssetField> FindChildren(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        int firstMatchIndex = -1;
        int lastMatchIndex = -1;
        int matchCount = 0;

        for (int index = 0; index < _children.Length; index++)
        {
            if (!string.Equals(_children[index].Name, name, StringComparison.Ordinal))
            {
                continue;
            }

            firstMatchIndex = firstMatchIndex < 0 ? index : firstMatchIndex;
            lastMatchIndex = index;
            matchCount++;
        }

        if (matchCount == 0)
        {
            return [];
        }

        if (matchCount == _children.Length)
        {
            return _children;
        }

        if (lastMatchIndex - firstMatchIndex + 1 == matchCount)
        {
            return new ArraySegment<AssetField>(_children, firstMatchIndex, matchCount);
        }

        var matches = new AssetField[matchCount];
        int matchIndex = 0;

        foreach (AssetField child in _children)
        {
            if (!string.Equals(child.Name, name, StringComparison.Ordinal))
            {
                continue;
            }

            matches[matchIndex] = child;
            matchIndex++;
        }

        return matches;
    }
}

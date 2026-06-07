namespace UnityAssetsPatcher.Core.Assets;

public static class AssetFieldPathNavigator
{
    public static TField? Find<TField>(
        TField root,
        string path,
        Func<TField, string> getName,
        Func<TField, IEnumerable<TField>> getChildren,
        Func<TField, string?> getValue)
        where TField : class
    {
        var segments = AssetFieldPath.Parse(path);

        if (segments is [{ HasSelector: false }])
        {
            return FindDescendantByName(root, segments[0].Name, getName, getChildren);
        }

        TField? current = root;

        foreach (AssetFieldPathSegment segment in segments)
        {
            current = FindChildBySegment(current, segment, getName, getChildren, getValue);

            if (current is null)
            {
                return null;
            }
        }

        return current;
    }

    private static TField? FindDescendantByName<TField>(
        TField field,
        string name,
        Func<TField, string> getName,
        Func<TField, IEnumerable<TField>> getChildren)
        where TField : class
    {
        if (string.Equals(getName(field), name, StringComparison.Ordinal))
        {
            return field;
        }

        return getChildren(field)
            .Select(child => FindDescendantByName(child, name, getName, getChildren))
            .OfType<TField>()
            .FirstOrDefault();
    }

    private static TField? FindChildBySegment<TField>(
        TField field,
        AssetFieldPathSegment segment,
        Func<TField, string> getName,
        Func<TField, IEnumerable<TField>> getChildren,
        Func<TField, string?> getValue)
        where TField : class
    {
        return getChildren(field).FirstOrDefault(child =>
            string.Equals(getName(child), segment.Name, StringComparison.Ordinal) &&
            MatchesSelector(child, segment, getName, getChildren, getValue));
    }

    private static bool MatchesSelector<TField>(
        TField field,
        AssetFieldPathSegment segment,
        Func<TField, string> getName,
        Func<TField, IEnumerable<TField>> getChildren,
        Func<TField, string?> getValue)
        where TField : class
    {
        if (!segment.HasSelector)
        {
            return true;
        }

        TField? selectorField = getChildren(field).FirstOrDefault(child =>
            string.Equals(getName(child), segment.SelectorFieldName, StringComparison.Ordinal));

        return selectorField is not null &&
               string.Equals(getValue(selectorField), segment.SelectorValue, StringComparison.Ordinal);
    }
}

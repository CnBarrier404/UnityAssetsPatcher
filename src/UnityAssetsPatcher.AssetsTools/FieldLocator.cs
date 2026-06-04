using AssetsTools.NET;
using UnityAssetsPatcher.Core.Assets;

namespace UnityAssetsPatcher.AssetsTools;

internal static class FieldLocator
{
    public static AssetTypeValueField? Find(AssetTypeValueField field, string path)
    {
        var segments = AssetFieldPath.Parse(path);

        if (segments is [{ HasSelector: false }])
        {
            return FindDescendantByName(field, segments[0].Name);
        }

        AssetTypeValueField? current = field;

        foreach (AssetFieldPathSegment segment in segments)
        {
            current = FindChildBySegment(current, segment);

            if (current is null)
            {
                return null;
            }
        }

        return current;
    }

    private static AssetTypeValueField? FindDescendantByName(AssetTypeValueField field, string name)
    {
        if (string.Equals(field.FieldName, name, StringComparison.Ordinal))
        {
            return field;
        }

        return field.Children
            .Select(child => FindDescendantByName(child, name))
            .OfType<AssetTypeValueField>()
            .FirstOrDefault();
    }

    private static AssetTypeValueField? FindChildBySegment(
        AssetTypeValueField field,
        AssetFieldPathSegment segment)
    {
        return field.Children.FirstOrDefault(child =>
            string.Equals(child.FieldName, segment.Name, StringComparison.Ordinal) &&
            MatchesSelector(child, segment));
    }

    private static bool MatchesSelector(AssetTypeValueField field, AssetFieldPathSegment segment)
    {
        if (!segment.HasSelector)
        {
            return true;
        }

        AssetTypeValueField? selectorField = field.Children.FirstOrDefault(child =>
            string.Equals(child.FieldName, segment.SelectorFieldName, StringComparison.Ordinal));

        return string.Equals(selectorField?.Value?.ToString(), segment.SelectorValue, StringComparison.Ordinal);
    }
}

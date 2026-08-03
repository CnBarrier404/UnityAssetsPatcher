using System.Text.Json;

namespace UnityAssetsPatcher.Application.Manifests;

internal static class ManifestJsonPointer
{
    public static string? PropertyName(string pointer)
    {
        int separatorIndex = pointer.LastIndexOf('/');

        if (separatorIndex < 0 || separatorIndex == pointer.Length - 1)
        {
            return null;
        }

        return DecodeSegment(pointer[(separatorIndex + 1)..]);
    }

    public static JsonElement? Resolve(JsonElement root, string pointer)
    {
        if (string.IsNullOrEmpty(pointer))
        {
            return root;
        }

        if (!pointer.StartsWith('/'))
        {
            return null;
        }

        JsonElement current = root;

        foreach (string encodedSegment in pointer[1..].Split('/'))
        {
            string segment = DecodeSegment(encodedSegment);

            if (current.ValueKind == JsonValueKind.Object && current.TryGetProperty(segment, out JsonElement property))
            {
                current = property;

                continue;
            }

            if (current.ValueKind != JsonValueKind.Array ||
                !int.TryParse(segment, out int index) ||
                index < 0 ||
                index >= current.GetArrayLength())
            {
                return null;
            }

            current = current[index];
        }

        return current;
    }

    public static JsonElement? ResolveSchemaConstraint(JsonElement schema, string pointer, string keyword)
    {
        string candidate = pointer;

        while (true)
        {
            var node = Resolve(schema, candidate);

            if (node is { ValueKind: JsonValueKind.Object } && node.Value.TryGetProperty(keyword, out _))
            {
                return node;
            }

            int separatorIndex = candidate.LastIndexOf('/');

            if (separatorIndex < 0)
            {
                return null;
            }

            candidate = candidate[..separatorIndex];
        }
    }

    public static string DecodeSegment(string segment)
    {
        return segment.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal);
    }
}

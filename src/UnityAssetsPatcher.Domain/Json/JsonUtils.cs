using System.Text.Json;

namespace UnityAssetsPatcher.Domain.Json;

public static class JsonUtils
{
    public static string FormatElementValue(JsonElement value)
    {
        return value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.GetRawText();
    }

    public static bool TryGetObjectValue(JsonElement value, out JsonElement objectValue)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                objectValue = value;
                return true;
            case JsonValueKind.Array when value.GetArrayLength() == 1:
            {
                JsonElement firstElement = value.EnumerateArray().Single();

                if (firstElement.ValueKind == JsonValueKind.Object)
                {
                    objectValue = firstElement;
                    return true;
                }

                break;
            }
            case JsonValueKind.Array:
            case JsonValueKind.Undefined:
            case JsonValueKind.String:
            case JsonValueKind.Number:
            case JsonValueKind.True:
            case JsonValueKind.False:
            case JsonValueKind.Null:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(value), value.ValueKind, "Unsupported JSON value kind.");
        }

        objectValue = default;
        return false;
    }
}

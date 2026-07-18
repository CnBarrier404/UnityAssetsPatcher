using System.Text;
using System.Text.Json;

namespace UnityAssetsPatcher.Application.Json;

public static class JsonUtils
{
    private const long MaxJsonFileSize = 10 * 1024 * 1024;

    public static JsonElement ParseElement(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    public static JsonElement ReadElementFromFile(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"JSON file not found: {path}", path);
        }

        FileInfo fileInfo = new(path);

        if (fileInfo.Length > MaxJsonFileSize)
        {
            throw new InvalidOperationException(
                $"JSON file '{path}' exceeds maximum allowed size of {MaxJsonFileSize} bytes.");
        }

        return ParseElement(File.ReadAllText(path, Encoding.UTF8));
    }

    public static JsonElement ReadElementFromStream(Stream stream, string sourceDescription)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDescription);

        using MemoryStream buffer = new();
        byte[] chunk = new byte[81920];
        long readBytes = 0;
        int bytesRead;

        while ((bytesRead = stream.Read(chunk, 0, chunk.Length)) > 0)
        {
            if (readBytes > MaxJsonFileSize - bytesRead)
            {
                throw new InvalidOperationException(
                    $"JSON source '{sourceDescription}' exceeds maximum allowed size of {MaxJsonFileSize} bytes.");
            }

            buffer.Write(chunk, 0, bytesRead);
            readBytes += bytesRead;
        }

        buffer.Position = 0;

        using JsonDocument document = JsonDocument.Parse(buffer);

        return document.RootElement.Clone();
    }

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
                throw new ArgumentOutOfRangeException();
        }

        objectValue = default;
        return false;
    }

    public static JsonElement ReadRequiredProperty(
        JsonElement element,
        string propertyName,
        JsonValueKind expectedKind,
        string ownerDescription)
    {
        return !element.TryGetProperty(propertyName, out JsonElement property) ||
               property.ValueKind != expectedKind
            ? throw new InvalidOperationException(
                $"{ownerDescription} must contain {FormatKindArticle(expectedKind)} {FormatKind(expectedKind)} '{propertyName}' property.")
            : property;
    }

    public static bool TryReadProperty(
        JsonElement element,
        string propertyName,
        JsonValueKind expectedKind,
        out JsonElement property)
    {
        if (!element.TryGetProperty(propertyName, out property))
        {
            return false;
        }

        return property.ValueKind != expectedKind
            ? throw new InvalidOperationException(
                $"Property '{propertyName}' must be {FormatKindArticle(expectedKind)} {FormatKind(expectedKind)}.")
            : true;
    }

    public static string ReadRequiredStringProperty(
        JsonElement element,
        string propertyName,
        string ownerDescription)
    {
        JsonElement property = ReadRequiredProperty(element, propertyName, JsonValueKind.String, ownerDescription);

        return property.GetString() ?? string.Empty;
    }

    public static string? ReadOptionalStringProperty(
        JsonElement element,
        string propertyName,
        string ownerDescription)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement property))
        {
            return null;
        }

        return property.ValueKind != JsonValueKind.String
            ? throw new InvalidOperationException($"{ownerDescription} '{propertyName}' property must be a string.")
            : property.GetString();
    }

    private static string FormatKind(JsonValueKind valueKind)
    {
        return valueKind switch
        {
            JsonValueKind.Object => "object",
            JsonValueKind.Array => "array",
            JsonValueKind.String => "string",
            JsonValueKind.Number => "number",
            JsonValueKind.True or JsonValueKind.False => "boolean",
            JsonValueKind.Null => "null",
            _ => valueKind.ToString().ToLowerInvariant(),
        };
    }

    private static string FormatKindArticle(JsonValueKind valueKind)
    {
        return valueKind is JsonValueKind.Object or JsonValueKind.Array ? "an" : "a";
    }
}

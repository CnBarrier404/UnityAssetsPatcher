using System.Text;
using System.Text.Json;

namespace UnityAssetsPatcher.Core.Utils;

public static class JsonUtils
{
    private static readonly JsonSerializerOptions DefaultOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static string Serialize<T>(T value, JsonSerializerOptions? options = null)
    {
        return JsonSerializer.Serialize(value, options ?? DefaultOptions);
    }

    public static T Deserialize<T>(string json, JsonSerializerOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(json);

        var result = JsonSerializer.Deserialize<T>(json, options ?? DefaultOptions);
        return result ?? throw new JsonException($"JSON cannot be deserialized to {typeof(T).Name}.");
    }

    public static JsonElement ParseElement(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    public static T ReadFromFile<T>(string path, JsonSerializerOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(path);

        return !File.Exists(path)
            ? throw new FileNotFoundException($"JSON file not found: {path}", path)
            : Deserialize<T>(File.ReadAllText(path, Encoding.UTF8), options);
    }

    public static JsonElement ReadElementFromFile(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        return !File.Exists(path)
            ? throw new FileNotFoundException($"JSON file not found: {path}", path)
            : ParseElement(File.ReadAllText(path, Encoding.UTF8));
    }

    public static void WriteToFile<T>(string path, T value, JsonSerializerOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(path);

        string? directory = Path.GetDirectoryName(path);

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, Serialize(value, options), Encoding.UTF8);
    }

    public static string FormatElementValue(JsonElement value)
    {
        return value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.GetRawText();
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

using System.Text;
using System.Text.Json;

namespace UnityAssetsPatcher.Utils;

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
}

using System.Text;
using System.Text.Json;

namespace UnityAssetsPatcher.Core.Json;

/// <summary>
/// Provides low-level helpers for reading, validating, and formatting JSON document values used by manifests and patch plans.
/// </summary>
/// <remarks>
/// This type deliberately works with <see cref="JsonDocument"/> and <see cref="JsonElement"/> instead of generic
/// <c>JsonSerializer.Serialize</c> or <c>JsonSerializer.Deserialize</c> APIs. Manifest patch values are dynamic JSON
/// tokens rather than stable DTOs, and avoiding serializer reflection keeps trimmed and NativeAOT builds reliable.
/// Use <see cref="JsonElementFactory"/> when new scalar or array values need to be constructed.
/// </remarks>
public static class JsonUtils
{
    /// <summary>
    /// Parses JSON text and returns a standalone copy of the root element.
    /// </summary>
    /// <param name="json">The JSON text to parse.</param>
    /// <returns>A cloned root <see cref="JsonElement"/> that remains valid after the internal document is disposed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="json"/> is <see langword="null"/>.</exception>
    /// <exception cref="JsonException">Thrown when <paramref name="json"/> is not valid JSON.</exception>
    public static JsonElement ParseElement(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    /// <summary>
    /// Reads a UTF-8 JSON file and returns a standalone copy of its root element.
    /// </summary>
    /// <param name="path">The path to the JSON file.</param>
    /// <returns>A cloned root <see cref="JsonElement"/> that can be safely stored beyond the file read operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="path"/> is <see langword="null"/>.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    /// <exception cref="JsonException">Thrown when the file content is not valid JSON.</exception>
    public static JsonElement ReadElementFromFile(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        return !File.Exists(path)
            ? throw new FileNotFoundException($"JSON file not found: {path}", path)
            : ParseElement(File.ReadAllText(path, Encoding.UTF8));
    }

    /// <summary>
    /// Formats a JSON value for terminal output and exception messages.
    /// </summary>
    /// <param name="value">The JSON value to format.</param>
    /// <returns>
    /// The unquoted string value for JSON strings, or the raw JSON text for numbers, booleans, objects, arrays, and null.
    /// </returns>
    /// <remarks>
    /// User-facing patch messages compare Unity field values against manifest values. String values are shown without
    /// JSON quotes to match how Unity scalar field values are displayed; non-string values keep their JSON shape.
    /// </remarks>
    public static string FormatElementValue(JsonElement value)
    {
        return value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.GetRawText();
    }

    /// <summary>
    /// Reads a required property and verifies that it has the expected JSON value kind.
    /// </summary>
    /// <param name="element">The object element that should contain the property.</param>
    /// <param name="propertyName">The property name to read.</param>
    /// <param name="expectedKind">The required JSON value kind.</param>
    /// <param name="ownerDescription">A short description used in validation error messages, such as <c>Manifest</c>.</param>
    /// <returns>The property value when present and of the expected kind.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the property is missing or has a different <see cref="JsonValueKind"/>.
    /// </exception>
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

    /// <summary>
    /// Attempts to read an optional property while still enforcing its value kind when present.
    /// </summary>
    /// <param name="element">The object element that may contain the property.</param>
    /// <param name="propertyName">The property name to read.</param>
    /// <param name="expectedKind">The required JSON value kind when the property exists.</param>
    /// <param name="property">The property value when the method returns <see langword="true"/>; otherwise the default value.</param>
    /// <returns><see langword="true"/> when the property exists; otherwise <see langword="false"/>.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the property exists but has a different <see cref="JsonValueKind"/>.
    /// </exception>
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

    /// <summary>
    /// Reads a required string property.
    /// </summary>
    /// <param name="element">The object element that should contain the property.</param>
    /// <param name="propertyName">The property name to read.</param>
    /// <param name="ownerDescription">A short description used in validation error messages, such as <c>Manifest</c>.</param>
    /// <returns>The string value, or an empty string when the JSON string token contains <see langword="null"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the property is missing or is not a JSON string.</exception>
    public static string ReadRequiredStringProperty(
        JsonElement element,
        string propertyName,
        string ownerDescription)
    {
        JsonElement property = ReadRequiredProperty(element, propertyName, JsonValueKind.String, ownerDescription);

        return property.GetString() ?? string.Empty;
    }

    /// <summary>
    /// Reads an optional string property.
    /// </summary>
    /// <param name="element">The object element that may contain the property.</param>
    /// <param name="propertyName">The property name to read.</param>
    /// <param name="ownerDescription">A short description used in validation error messages, such as <c>Manifest</c>.</param>
    /// <returns>The string value when present; otherwise <see langword="null"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the property exists but is not a JSON string.</exception>
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

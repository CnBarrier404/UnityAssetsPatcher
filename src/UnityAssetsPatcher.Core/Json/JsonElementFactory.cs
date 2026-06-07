using System.Text.Json;

namespace UnityAssetsPatcher.Core.Json;

/// <summary>
/// Creates standalone <see cref="JsonElement"/> values without using reflection-based JSON serialization.
/// </summary>
/// <remarks>
/// The patch pipeline stores manifest match and write values as <see cref="JsonElement"/> because those values are
/// schema-flexible: a field value can be a string, number, boolean, array, or a small resolver object depending on the
/// Unity field being patched. These helpers intentionally use <see cref="Utf8JsonWriter"/> instead of
/// <c>JsonSerializer.SerializeToElement</c> so trimmed and NativeAOT builds do not depend on runtime serializer
/// metadata. Returned elements are cloned from their backing document and remain usable after this factory returns.
/// </remarks>
public static class JsonElementFactory
{
    /// <summary>
    /// Creates a standalone JSON string element.
    /// </summary>
    /// <param name="value">The .NET string value to write as a JSON string token.</param>
    /// <returns>A cloned <see cref="JsonElement"/> whose <see cref="JsonElement.ValueKind"/> is <see cref="JsonValueKind.String"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <see langword="null"/>.</exception>
    public static JsonElement String(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return Create(writer => writer.WriteStringValue(value));
    }

    /// <summary>
    /// Creates a standalone JSON boolean element.
    /// </summary>
    /// <param name="value">The boolean value to write as a JSON <c>true</c> or <c>false</c> token.</param>
    /// <returns>A cloned <see cref="JsonElement"/> whose value kind is <see cref="JsonValueKind.True"/> or <see cref="JsonValueKind.False"/>.</returns>
    public static JsonElement Boolean(bool value)
    {
        return Create(writer => writer.WriteBooleanValue(value));
    }

    /// <summary>
    /// Creates a standalone JSON number element from a signed integer.
    /// </summary>
    /// <param name="value">The signed integer value to write as a JSON number token.</param>
    /// <returns>A cloned <see cref="JsonElement"/> whose <see cref="JsonElement.ValueKind"/> is <see cref="JsonValueKind.Number"/>.</returns>
    public static JsonElement Number(long value)
    {
        return Create(writer => writer.WriteNumberValue(value));
    }

    /// <summary>
    /// Creates a standalone JSON number element from an unsigned integer.
    /// </summary>
    /// <param name="value">The unsigned integer value to write as a JSON number token.</param>
    /// <returns>A cloned <see cref="JsonElement"/> whose <see cref="JsonElement.ValueKind"/> is <see cref="JsonValueKind.Number"/>.</returns>
    public static JsonElement Number(ulong value)
    {
        return Create(writer => writer.WriteNumberValue(value));
    }

    /// <summary>
    /// Creates a standalone JSON number element from a floating-point value.
    /// </summary>
    /// <param name="value">The floating-point value to write as a JSON number token.</param>
    /// <returns>A cloned <see cref="JsonElement"/> whose <see cref="JsonElement.ValueKind"/> is <see cref="JsonValueKind.Number"/>.</returns>
    /// <exception cref="ArgumentException">Thrown by <see cref="Utf8JsonWriter"/> when <paramref name="value"/> is not a valid JSON number.</exception>
    public static JsonElement Number(double value)
    {
        return Create(writer => writer.WriteNumberValue(value));
    }

    /// <summary>
    /// Creates a standalone JSON array element from existing JSON elements.
    /// </summary>
    /// <param name="values">The elements to copy into the new JSON array, in order.</param>
    /// <returns>A cloned <see cref="JsonElement"/> whose <see cref="JsonElement.ValueKind"/> is <see cref="JsonValueKind.Array"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="values"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// Each source element is written with <see cref="JsonElement.WriteTo(Utf8JsonWriter)"/>, so nested objects and
    /// arrays are preserved exactly as JSON values rather than interpreted as CLR objects.
    /// </remarks>
    public static JsonElement Array(IEnumerable<JsonElement> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        return Create(writer =>
        {
            writer.WriteStartArray();

            foreach (JsonElement value in values)
            {
                value.WriteTo(writer);
            }

            writer.WriteEndArray();
        });
    }

    private static JsonElement Create(Action<Utf8JsonWriter> write)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            write(writer);
        }

        using JsonDocument document = JsonDocument.Parse(stream.ToArray());
        return document.RootElement.Clone();
    }
}

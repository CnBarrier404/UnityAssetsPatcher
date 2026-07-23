using System.Text.Json;

namespace UnityAssetsPatcher.Domain.Json;

public static class JsonElementFactory
{
    public static JsonElement String(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return Create(writer => writer.WriteStringValue(value));
    }

    public static JsonElement Boolean(bool value)
    {
        return Create(writer => writer.WriteBooleanValue(value));
    }

    public static JsonElement Number(long value)
    {
        return Create(writer => writer.WriteNumberValue(value));
    }

    public static JsonElement Array(IEnumerable<JsonElement> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        return ArrayFromWriter(writer =>
        {
            foreach (JsonElement value in values)
            {
                value.WriteTo(writer);
            }
        });
    }

    public static JsonElement ArrayFromWriter(Action<Utf8JsonWriter> writeElements)
    {
        ArgumentNullException.ThrowIfNull(writeElements);

        return Create(writer =>
        {
            writer.WriteStartArray();
            writeElements(writer);
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

        using JsonDocument document = stream.TryGetBuffer(out var buffer)
            ? JsonDocument.Parse(buffer.AsMemory())
            : JsonDocument.Parse(stream.ToArray());

        return document.RootElement.Clone();
    }
}

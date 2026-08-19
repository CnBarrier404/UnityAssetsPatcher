using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Application.Repository;

namespace UnityAssetsPatcher.Infrastructure.Repository;

internal sealed class RepositoryJsonPersistence
{
    private readonly IFileSystemOperations _fileSystemOperations;

    public RepositoryJsonPersistence(IFileSystemOperations fileSystemOperations)
    {
        ArgumentNullException.ThrowIfNull(fileSystemOperations);

        _fileSystemOperations = fileSystemOperations;
    }

    public T Read<T>(string path, JsonTypeInfo<T> typeInfo, string description)
    {
        try
        {
            using Stream stream = _fileSystemOperations.OpenRead(path);

            return JsonSerializer.Deserialize(stream, typeInfo) ??
                   throw new InvalidDataException($"{description} could not be read: {path}");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"{description} contains invalid JSON: {path}", exception);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException
                                              or NotSupportedException or OverflowException)
        {
            throw new InvalidDataException($"{description} contains invalid data: {path}", exception);
        }
    }

    public void Write<T>(
        string path,
        T value,
        JsonTypeInfo<T> typeInfo,
        FileDestinationMode mode)
    {
        _fileSystemOperations.WriteFileAtomically(
            path,
            mode,
            stream => JsonSerializer.Serialize(stream, value, typeInfo));
    }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(RepositoryDocument))]
[JsonSerializable(typeof(RepositoryTransaction))]
[JsonSerializable(typeof(BaseCatalog))]
[JsonSerializable(typeof(LayerRecord))]
internal sealed partial class RepositoryJsonContext : JsonSerializerContext;

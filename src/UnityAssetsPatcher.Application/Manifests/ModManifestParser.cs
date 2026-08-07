using System.Text;
using System.Text.Json;
using UnityAssetsPatcher.Application.Operations;

namespace UnityAssetsPatcher.Application.Manifests;

internal static class ModManifestParser
{
    public static OperationResult<ModManifest> Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        return Parse(Encoding.UTF8.GetBytes(json));
    }

    public static OperationResult<ModManifest> Parse(ReadOnlySpan<byte> utf8Json)
    {
        if (utf8Json is [0xef, 0xbb, 0xbf, ..])
        {
            utf8Json = utf8Json[3..];
        }

        try
        {
            using JsonDocument jsonDocument = JsonDocument.Parse(utf8Json.ToArray());
            JsonElement root = jsonDocument.RootElement;
            OperationError? schemaError = ManifestSchemaValidator.Validate(root);

            if (schemaError is not null)
            {
                return new OperationFailed<ModManifest>(schemaError);
            }

            ManifestDocumentDto? document = root.Deserialize(
                ManifestJsonSerializerContext.Default.ManifestDocumentDto);

            if (document is null)
            {
                return InvalidJson();
            }

            OperationError? semanticError = ManifestSemanticValidator.Validate(document);

            if (semanticError is not null)
            {
                return new OperationFailed<ModManifest>(semanticError);
            }

            ModManifest manifest = ModManifestMapper.Map(document);

            return new OperationSucceeded<ModManifest>(manifest);
        }
        catch (JsonException exception)
        {
            return InvalidJson(exception);
        }
    }

    private static OperationFailed<ModManifest> InvalidJson(JsonException? exception = null)
    {
        var parameters = new Dictionary<string, object?>(StringComparer.Ordinal);

        if (exception?.LineNumber is { } lineNumber)
        {
            parameters.Add("line_number", lineNumber);
        }

        if (exception?.BytePositionInLine is { } bytePosition)
        {
            parameters.Add("byte_position", bytePosition);
        }

        return new OperationFailed<ModManifest>(new OperationError(ManifestErrorCodes.InvalidJson, parameters));
    }
}

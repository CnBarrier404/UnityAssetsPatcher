using System.Text;
using System.Text.Json;
using UnityAssetsPatcher.Application.Operations;

namespace UnityAssetsPatcher.Application.Mods;

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

        JsonDocument jsonDocument;

        try
        {
            jsonDocument = JsonDocument.Parse(utf8Json.ToArray());
        }
        catch (JsonException exception)
        {
            return new OperationFailed<ModManifest>(InvalidJson(exception));
        }

        using (jsonDocument)
        {
            return Parse(jsonDocument.RootElement);
        }
    }

    private static OperationResult<ModManifest> Parse(JsonElement root)
    {
        OperationError? schemaError = ManifestSchemaValidator.Validate(root);

        if (schemaError is not null)
        {
            return new OperationFailed<ModManifest>(schemaError);
        }

        ManifestDocumentDto? document = root.Deserialize(
            ManifestJsonSerializerContext.Default.ManifestDocumentDto);

        if (document is null)
        {
            return new OperationFailed<ModManifest>(new OperationError(ManifestErrorCodes.InvalidJson));
        }

        OperationError? semanticError = ManifestSemanticValidator.Validate(document);

        if (semanticError is not null)
        {
            return new OperationFailed<ModManifest>(semanticError);
        }

        ModManifest manifest = ModManifestMapper.Map(document);

        return new OperationSucceeded<ModManifest>(manifest);
    }

    private static OperationError InvalidJson(JsonException exception)
    {
        var parameters = new Dictionary<string, object?>(StringComparer.Ordinal);

        if (exception.BytePositionInLine is { } bytePosition)
        {
            parameters.Add("byte_position", bytePosition);
        }

        if (exception.LineNumber is { } lineNumber)
        {
            parameters.Add("line_number", lineNumber);
        }

        return new OperationError(ManifestErrorCodes.InvalidJson, parameters);
    }
}

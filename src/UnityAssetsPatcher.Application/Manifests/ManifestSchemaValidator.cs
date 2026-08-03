using System.Reflection;
using System.Text.Json;
using Json.Schema;
using UnityAssetsPatcher.Application.Operations;

namespace UnityAssetsPatcher.Application.Manifests;

internal static class ManifestSchemaValidator
{
    private sealed record ManifestSchemaDefinition(JsonSchema Schema, JsonElement Source);

    public const string CurrentSchema = "https://uap.cnbarrier.com/schema-v1.json";

    private const string ResourceName = "UnityAssetsPatcher.Application.Manifests.schema-v1.json";

    private static readonly Lazy<ManifestSchemaDefinition> Definition = new(
        LoadDefinition,
        LazyThreadSafetyMode.ExecutionAndPublication);

    public static OperationError? Validate(JsonElement instance)
    {
        ManifestSchemaDefinition definition = Definition.Value;
        var options = new EvaluationOptions
        {
            OutputFormat = OutputFormat.Hierarchical,
        };
        EvaluationResults results = definition.Schema.Evaluate(instance, options);

        return results.IsValid ? null : ManifestSchemaErrorMapper.Map(definition.Source, instance, results);
    }

    private static ManifestSchemaDefinition LoadDefinition()
    {
        Assembly assembly = typeof(ManifestSchemaValidator).Assembly;
        using Stream stream = assembly.GetManifestResourceStream(ResourceName) ??
                              throw new InvalidOperationException(
                                  $"Embedded manifest schema '{ResourceName}' was not found.");
        using var reader = new StreamReader(stream);
        string schemaText = reader.ReadToEnd();
        using JsonDocument document = JsonDocument.Parse(schemaText);
        JsonElement source = document.RootElement.Clone();
        var schema = JsonSchema.Build(source, new BuildOptions
        {
            Dialect = Dialect.Draft202012,
        });

        return new ManifestSchemaDefinition(schema, source);
    }
}

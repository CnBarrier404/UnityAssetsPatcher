using Json.Schema;

namespace UnityAssetsPatcher.Application.Mods;

internal sealed record ManifestSchemaFailure(
    EvaluationResults Result,
    KeyValuePair<string, string> Error);

internal static class ManifestSchemaFailureSelector
{
    public static ManifestSchemaFailure? Select(EvaluationResults results)
    {
        ArgumentNullException.ThrowIfNull(results);

        var candidates = new List<ManifestSchemaFailure>();

        CollectFailures(results, candidates);

        return candidates.OrderBy(ErrorPriority).FirstOrDefault();
    }

    private static void CollectFailures(
        EvaluationResults result,
        ICollection<ManifestSchemaFailure> failures)
    {
        if (result.IsValid)
        {
            return;
        }

        if (result.Errors is not null)
        {
            foreach (var error in result.Errors)
            {
                failures.Add(new ManifestSchemaFailure(result, error));
            }
        }

        if (result.Details is null)
        {
            return;
        }

        foreach (EvaluationResults detail in result.Details)
        {
            CollectFailures(detail, failures);
        }
    }

    private static int ErrorPriority(ManifestSchemaFailure candidate)
    {
        string schemaPath = candidate.Result.SchemaLocation.Fragment;

        if (schemaPath.Contains("/if/", StringComparison.Ordinal) ||
            schemaPath.Contains("/not/", StringComparison.Ordinal))
        {
            return 4;
        }

        return candidate.Error.Key switch
        {
            "type" or "required" or "const" => 0,
            "minLength" or "minItems" or "minProperties" or "pattern" => 1,
            "not" => 2,
            _ => 3
        };
    }
}

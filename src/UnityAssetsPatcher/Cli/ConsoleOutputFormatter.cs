using UnityAssetsPatcher.Core;

namespace UnityAssetsPatcher.Cli;

public sealed class ConsoleOutputFormatter
{
    public static void WriteAssetSummary(TextWriter output, IReadOnlyList<AssetsInfo> assets, int? limit)
    {
        var assetsToPrint = limit is null ? assets : assets.Take(limit.Value);

        output.WriteLine($"{"Path ID",12} | {"Type ID",7} | {"Type Name",-24} | {"Byte Size",10}");
        output.WriteLine(new string('-', 64));

        foreach (AssetsInfo asset in assetsToPrint)
        {
            output.WriteLine($"{asset.PathId,12} | {asset.TypeId,7} | {asset.TypeName,-24} | {asset.ByteSize,10}");
        }

        if (limit is null || assets.Count <= limit.Value)
        {
            return;
        }

        output.WriteLine();
        output.WriteLine(
            $"Showing {limit.Value} of {assets.Count} assets. Use --all to print every row or --limit <count> to choose a different limit.");
    }

    public static void WriteAssetFields(TextWriter output, AssetsFieldInfo fieldTree)
    {
        WriteAssetField(output, fieldTree, 0);
    }

    public static void WriteFindResults(TextWriter output, IReadOnlyList<AssetMatch> matches)
    {
        output.WriteLine($"{"Path ID",12} | {"Type ID",7} | {"Type Name",-24} | Matched Fields");
        output.WriteLine(new string('-', 86));

        foreach (AssetMatch match in matches)
        {
            string matchedFields = string.Join(", ",
                match.IncludeGroup.Select(condition =>
                    $"{condition.Key}={AssetFieldMatcher.FormatJsonValue(condition.Value)}"));
            output.WriteLine(
                $"{match.Asset.PathId,12} | {match.Asset.TypeId,7} | {match.Asset.TypeName,-24} | {matchedFields}");
        }
    }

    public static void WritePatchPreview(TextWriter output, PatchPreviewResult preview)
    {
        output.WriteLine("DRY RUN");

        foreach (PatchPreviewAssetResult assetResult in preview.Assets)
        {
            output.WriteLine($"Path ID: {assetResult.Asset.PathId} ({assetResult.Asset.TypeName})");

            foreach (PatchPreviewOperationResult operation in assetResult.Operations)
            {
                if (!operation.WillChange)
                {
                    output.WriteLine(
                        $"  {operation.Path}: skipped, current value {operation.OldValue} does not match expected {AssetFieldMatcher.FormatJsonValue(operation.From)}");
                    continue;
                }

                output.WriteLine(
                    $"  {operation.Path}: {operation.OldValue} -> {AssetFieldMatcher.FormatJsonValue(operation.To)}");
            }
        }
    }

    private static void WriteAssetField(TextWriter output, AssetsFieldInfo field, int depth)
    {
        string indentation = new(' ', depth * 2);
        string value = field.Value is null ? string.Empty : $": {field.Value}";
        output.WriteLine($"{indentation}{field.Name} ({field.TypeName}){value}");

        foreach (AssetsFieldInfo child in field.Children)
        {
            WriteAssetField(output, child, depth + 1);
        }
    }
}

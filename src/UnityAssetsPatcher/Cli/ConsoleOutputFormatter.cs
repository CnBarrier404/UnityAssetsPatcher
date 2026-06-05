using System.Globalization;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Core.Assets;
using UnityAssetsPatcher.Core.Utils;

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
                    $"{condition.Key}={JsonUtils.FormatElementValue(condition.Value)}"));
            output.WriteLine(
                $"{match.Asset.PathId,12} | {match.Asset.TypeId,7} | {match.Asset.TypeName,-24} | {matchedFields}");
        }
    }

    public static void WritePatchPreview(TextWriter output, PatchPreviewResult preview)
    {
        output.WriteLine("DRY RUN");
        WritePatchPreviewAssets(output, preview);
    }

    public static void WriteInstallPreview(TextWriter output, InstallPreviewResult result, TimeSpan elapsed)
    {
        output.WriteLine("DRY RUN");
        output.WriteLine($"Mod: {result.ModName} {result.ModVersion}");
        output.WriteLine($"Files: {result.Files.Count}");
        output.WriteLine($"Copied files: {result.CopiedFiles.Count}");
        output.WriteLine($"Assets: {result.Files.Sum(file => file.Preview.Assets.Count)}");
        output.WriteLine(
            $"Operations: {result.Files.Sum(file => file.Preview.Assets.Sum(asset => asset.Operations.Count))}");
        output.WriteLine($"Elapsed: {FormatElapsedSeconds(elapsed)} s");

        foreach (InstallCopyFilePreviewResult copiedFile in result.CopiedFiles)
        {
            string suffix = copiedFile.WillCopy ? string.Empty : " (skipped, destination exists)";
            output.WriteLine($"{copiedFile.Source} -> {copiedFile.DestinationPath}{suffix}");
        }

        foreach (InstallPreviewFileResult file in result.Files)
        {
            output.WriteLine($"{file.Target}: {file.AssetsFilePath}");
            WritePatchPreviewAssets(output, file.Preview);
        }
    }

    private static void WritePatchPreviewAssets(TextWriter output, PatchPreviewResult preview)
    {
        foreach (PatchPreviewAssetResult assetResult in preview.Assets)
        {
            output.WriteLine($"Path ID: {assetResult.Asset.PathId} ({assetResult.Asset.TypeName})");

            foreach (PatchPreviewOperationResult operation in assetResult.Operations)
            {
                if (!operation.WillChange)
                {
                    output.WriteLine(
                        $"  {operation.Path}: skipped, current value {operation.OldValue} does not match expected {JsonUtils.FormatElementValue(operation.From)}");
                    continue;
                }

                output.WriteLine(
                    $"  {operation.Path}: {operation.OldValue} -> {JsonUtils.FormatElementValue(operation.To)}");
            }
        }
    }

    public static void WritePatchApply(TextWriter output, PatchApplyResult result)
    {
        output.WriteLine("APPLIED");
        output.WriteLine($"Output: {result.OutputPath}");

        if (result.BackupPath is not null)
        {
            output.WriteLine($"Backup: {result.BackupPath}");
        }

        output.WriteLine($"Assets: {result.AssetCount}");
        output.WriteLine($"Operations: {result.OperationCount}");
    }

    public static void WriteInstallResult(TextWriter output, InstallModResult result, TimeSpan elapsed)
    {
        output.WriteLine("INSTALLED");
        output.WriteLine($"Mod: {result.ModName} {result.ModVersion}");
        output.WriteLine($"Files: {result.Files.Count}");
        output.WriteLine($"Copied files: {result.CopiedFiles.Count}");
        output.WriteLine($"Assets: {result.Files.Sum(file => file.AssetCount)}");
        output.WriteLine($"Operations: {result.Files.Sum(file => file.OperationCount)}");
        output.WriteLine($"Elapsed: {FormatElapsedSeconds(elapsed)} s");

        foreach (InstallCopiedFileResult copiedFile in result.CopiedFiles)
        {
            output.WriteLine($"{copiedFile.Source} -> {copiedFile.DestinationPath}");
        }

        foreach (InstallModFileResult file in result.Files)
        {
            output.WriteLine($"{file.Target}: {file.AssetsFilePath}");
            output.WriteLine($"  Backup: {file.BackupPath}");
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

    private static string FormatElapsedSeconds(TimeSpan elapsed)
    {
        return elapsed.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture);
    }
}

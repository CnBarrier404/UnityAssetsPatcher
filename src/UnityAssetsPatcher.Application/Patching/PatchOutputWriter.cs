using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Assets;

namespace UnityAssetsPatcher.Application.Patching;

public sealed class PatchOutputWriter
{
    private readonly IAssetsFileWriter _assetsPatchWriter;

    public PatchOutputWriter(IAssetsFileWriter assetsPatchWriter)
    {
        _assetsPatchWriter = assetsPatchWriter;
    }

    public PatchApplyResult Write(
        string assetsFilePath,
        string outputPath,
        PatchPlan plan)
    {
        return plan switch
        {
            AssetReplacementPlan replacementPlan =>
                WriteReplacements(assetsFilePath, outputPath, replacementPlan.Replacements),
            FieldPatchPlan fieldPlan => WriteFieldPatch(assetsFilePath, outputPath, fieldPlan.Assets),
            _ => throw new ArgumentOutOfRangeException(nameof(plan)),
        };
    }

    public PatchApplyResult WriteFieldPatch(
        string assetsFilePath,
        string outputPath,
        IReadOnlyList<AssetFieldPatch> plan)
    {
        WriteTarget target = ResolveWriteTarget(assetsFilePath, outputPath);
        var changedPlan = plan
            .Where(asset => asset.Operations.Count > 0)
            .ToArray();

        if (changedPlan.Length == 0)
        {
            return new PatchApplyResult(target.OutputPath, null, 0, 0);
        }

        _assetsPatchWriter.WriteFieldPatches(assetsFilePath, target.OutputPath, changedPlan);

        return new PatchApplyResult(
            target.OutputPath,
            null,
            changedPlan.Length,
            changedPlan.Sum(asset => asset.Operations.Count));
    }

    public PatchApplyResult WriteReplacements(
        string assetsFilePath,
        string outputPath,
        IReadOnlyList<AssetReplacement> plan)
    {
        WriteTarget target = ResolveWriteTarget(assetsFilePath, outputPath);

        _assetsPatchWriter.WriteReplacements(assetsFilePath, target.OutputPath, plan);

        return new PatchApplyResult(target.OutputPath, null, plan.Count, plan.Count);
    }

    private static WriteTarget ResolveWriteTarget(string assetsFilePath, string outputPath)
    {
        if (!File.Exists(assetsFilePath))
        {
            throw new FileNotFoundException($"Assets file not found: {assetsFilePath}", assetsFilePath);
        }

        bool overwritesInput = string.Equals(
            Path.GetFullPath(outputPath),
            Path.GetFullPath(assetsFilePath),
            StringComparison.OrdinalIgnoreCase);

        if (overwritesInput)
        {
            throw new InvalidOperationException("--output cannot point to the input assets file.");
        }

        if (!overwritesInput && File.Exists(outputPath))
        {
            throw new IOException($"Output file already exists: {outputPath}");
        }

        return new WriteTarget(outputPath);
    }

    private sealed record WriteTarget(string OutputPath);
}

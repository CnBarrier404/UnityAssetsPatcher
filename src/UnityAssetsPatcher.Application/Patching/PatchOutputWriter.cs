using UnityAssetsPatcher.Application.Assets;
using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Domain.Assets;

namespace UnityAssetsPatcher.Application.Patching;

public sealed class PatchOutputWriter
{
    private readonly IAssetsFileWriter _assetsPatchWriter;
    private readonly IFileSystemOperations _fileSystemOperations;

    public PatchOutputWriter(
        IAssetsFileWriter assetsPatchWriter,
        IFileSystemOperations fileSystemOperations)
    {
        ArgumentNullException.ThrowIfNull(assetsPatchWriter);
        ArgumentNullException.ThrowIfNull(fileSystemOperations);
        _assetsPatchWriter = assetsPatchWriter;
        _fileSystemOperations = fileSystemOperations;
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
            FieldPatchAndCopyPlan copyPlan =>
                WriteFieldPatchesAndCopies(assetsFilePath, outputPath, copyPlan),
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

    private WriteTarget ResolveWriteTarget(string assetsFilePath, string outputPath)
    {
        if (!File.Exists(assetsFilePath))
        {
            throw new FileNotFoundException($"Assets file not found: {assetsFilePath}", assetsFilePath);
        }

        bool overwritesInput = _fileSystemOperations.PathsEqual(outputPath, assetsFilePath);

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

    private PatchApplyResult WriteFieldPatchesAndCopies(
        string assetsFilePath,
        string outputPath,
        FieldPatchAndCopyPlan plan)
    {
        WriteTarget target = ResolveWriteTarget(assetsFilePath, outputPath);
        var changedPatches = plan.FieldPatches
            .Where(asset => asset.Operations.Count > 0)
            .ToArray();
        _assetsPatchWriter.WriteFieldPatchesAndCopies(
            assetsFilePath,
            target.OutputPath,
            changedPatches,
            plan.Copies);

        return new PatchApplyResult(
            target.OutputPath,
            null,
            changedPatches.Select(asset => asset.PathId)
                .Concat(plan.Copies.Select(copy => copy.TargetPathId))
                .Distinct()
                .Count(),
            changedPatches.Sum(asset => asset.Operations.Count) + plan.Copies.Count);
    }

    private sealed record WriteTarget(string OutputPath);
}

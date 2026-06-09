using UnityAssetsPatcher.Core.Assets;

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
        string? outputPathOption,
        string backupDirectory,
        PatchFileWritePlan plan)
    {
        return plan.Kind == PatchFileWritePlanKind.Replacement
            ? WriteReplacements(assetsFilePath, outputPathOption, backupDirectory, plan.Replacements)
            : WriteFieldPatch(assetsFilePath, outputPathOption, backupDirectory, plan.Assets);
    }

    public PatchApplyResult WriteFieldPatch(
        string assetsFilePath,
        string? outputPathOption,
        string backupDirectory,
        IReadOnlyList<AssetFieldPatch> plan)
    {
        WriteTarget target = ResolveWriteTarget(assetsFilePath, outputPathOption);
        var changedPlan = plan
            .Where(asset => asset.Operations.Count > 0)
            .ToArray();

        if (changedPlan.Length == 0)
        {
            return new PatchApplyResult(target.OutputPath, null, 0, 0);
        }

        string? backupPath = CreateBackupIfNeeded(target, backupDirectory);
        _assetsPatchWriter.WritePatch(assetsFilePath, target.OutputPath, changedPlan);

        return new PatchApplyResult(
            target.OutputPath,
            backupPath,
            changedPlan.Length,
            changedPlan.Sum(asset => asset.Operations.Count));
    }

    public PatchApplyResult WriteReplacements(
        string assetsFilePath,
        string? outputPathOption,
        string backupDirectory,
        IReadOnlyList<AssetReplacement> plan)
    {
        WriteTarget target = ResolveWriteTarget(assetsFilePath, outputPathOption);
        string? backupPath = CreateBackupIfNeeded(target, backupDirectory);

        _assetsPatchWriter.WriteReplacements(assetsFilePath, target.OutputPath, plan);

        return new PatchApplyResult(target.OutputPath, backupPath, plan.Count, plan.Count);
    }

    private static WriteTarget ResolveWriteTarget(string assetsFilePath, string? outputPathOption)
    {
        if (!File.Exists(assetsFilePath))
        {
            throw new FileNotFoundException($"Assets file not found: {assetsFilePath}", assetsFilePath);
        }

        string outputPath = outputPathOption ?? assetsFilePath;
        bool overwritesInput = string.Equals(
            Path.GetFullPath(outputPath),
            Path.GetFullPath(assetsFilePath),
            StringComparison.OrdinalIgnoreCase);

        if (outputPathOption is not null && overwritesInput)
        {
            throw new InvalidOperationException("--output cannot point to the input assets file.");
        }

        if (!overwritesInput && File.Exists(outputPath))
        {
            throw new IOException($"Output file already exists: {outputPath}");
        }

        return new WriteTarget(assetsFilePath, outputPath, overwritesInput);
    }

    private static string? CreateBackupIfNeeded(WriteTarget target, string backupDirectory)
    {
        if (!target.OverwritesInput)
        {
            return null;
        }

        Directory.CreateDirectory(backupDirectory);
        string backupPath = CreateBackupPath(backupDirectory, target.AssetsFilePath);
        File.Copy(target.AssetsFilePath, backupPath, false);

        return backupPath;
    }

    private static string CreateBackupPath(string backupDirectory, string inputPath)
    {
        string fileName = Path.GetFileNameWithoutExtension(inputPath);
        string extension = Path.GetExtension(inputPath);
        string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        string candidate = Path.Combine(backupDirectory, $"{fileName}.{timestamp}{extension}");

        for (int index = 1; File.Exists(candidate); index++)
        {
            candidate = Path.Combine(backupDirectory, $"{fileName}.{timestamp}.{index}{extension}");
        }

        return candidate;
    }

    private sealed record WriteTarget(string AssetsFilePath, string OutputPath, bool OverwritesInput);
}

public sealed record PatchApplyResult(string OutputPath, string? BackupPath, int AssetCount, int OperationCount);

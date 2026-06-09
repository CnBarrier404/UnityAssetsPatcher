using AssetsTools.NET;
using UnityAssetsPatcher.Core.Assets;
using AssetsToolsNetFileWriter = AssetsTools.NET.AssetsFileWriter;

namespace UnityAssetsPatcher.AssetsTools;

public sealed class AssetsFileWriter : IAssetsFileWriter
{
    private readonly string _tpkFilePath;

    public AssetsFileWriter(string tpkFilePath)
    {
        _tpkFilePath = tpkFilePath;
    }

    public void WritePatch(string inputPath, string outputPath, IReadOnlyList<AssetFieldPatch> plan)
    {
        WriteAssetsFile(inputPath, outputPath, session => ApplyPatchPlan(session, plan));
    }

    public void WriteReplacements(string inputPath, string outputPath, IReadOnlyList<AssetReplacement> plan)
    {
        ValidateReplacementSources(plan);
        WriteAssetsFile(inputPath, outputPath, session => ApplyReplacementPlan(session, plan));
    }

    private void WriteAssetsFile(string inputPath, string outputPath, Action<AssetsFileSession> applyChanges)
    {
        string? outputDirectory = Path.GetDirectoryName(outputPath);
        string tempPath = CreateTempPath(outputPath, outputDirectory);

        try
        {
            using (AssetsFileSession session = AssetsFileSession.Open(inputPath, _tpkFilePath))
            {
                if (!string.IsNullOrEmpty(outputDirectory))
                {
                    Directory.CreateDirectory(outputDirectory);
                }

                applyChanges(session);
                WriteSessionToFile(session, tempPath);
            }

            File.Move(tempPath, outputPath, overwrite: true);
        }
        finally
        {
            DeleteIfExists(tempPath);
        }
    }

    private static string CreateTempPath(string outputPath, string? outputDirectory)
    {
        return Path.Combine(
            string.IsNullOrEmpty(outputDirectory) ? Directory.GetCurrentDirectory() : outputDirectory,
            $".{Path.GetFileName(outputPath)}.{Guid.NewGuid():N}.tmp");
    }

    private static void ValidateReplacementSources(IReadOnlyList<AssetReplacement> plan)
    {
        foreach (string sourceAssetsFilePath in plan
                     .Select(replacement => replacement.SourceAssetsFilePath)
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!File.Exists(sourceAssetsFilePath))
            {
                throw new FileNotFoundException(
                    $"Assets file not found: {sourceAssetsFilePath}",
                    sourceAssetsFilePath);
            }
        }
    }

    private static void WriteSessionToFile(AssetsFileSession session, string outputPath)
    {
        using FileStream outputStream = File.Create(outputPath);
        var writer = new AssetsToolsNetFileWriter(outputStream);
        session.AssetsFile.Write(writer);
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static void ApplyPatchPlan(AssetsFileSession session, IReadOnlyList<AssetFieldPatch> plan)
    {
        foreach (AssetFieldPatch asset in plan)
        {
            AssetTypeValueField baseField = session.Manager.GetBaseField(session.AssetsFileInstance, asset.PathId);

            if (baseField.IsDummy)
            {
                throw new InvalidOperationException($"Asset not found or cannot be read: {asset.PathId}");
            }

            AssetTypeValueField mutableField = baseField.Clone();

            foreach (FieldPatchOperation operation in asset.Operations)
            {
                AssetTypeValueField targetField = AssetsFieldLocator.Find(mutableField, operation.Path)
                                                  ?? throw new InvalidOperationException(
                                                      $"Field not found for Path ID {asset.PathId}: {operation.Path}");
                AssetsFieldWriter.WriteJsonValue(targetField, operation.To);
            }

            AssetFileInfo assetInfo = session.AssetsFile.GetAssetInfo(asset.PathId);
            assetInfo.SetNewData(mutableField);
        }
    }

    private void ApplyReplacementPlan(AssetsFileSession targetSession, IReadOnlyList<AssetReplacement> plan)
    {
        foreach (var sourceGroup in plan.GroupBy(replacement => replacement.SourceAssetsFilePath,
                     StringComparer.OrdinalIgnoreCase))
        {
            using AssetsFileSession sourceSession = AssetsFileSession.Open(sourceGroup.Key, _tpkFilePath);

            foreach (AssetReplacement replacement in sourceGroup)
            {
                AssetTypeValueField sourceField = sourceSession.Manager.GetBaseField(
                    sourceSession.AssetsFileInstance,
                    replacement.SourcePathId);

                if (sourceField.IsDummy)
                {
                    throw new InvalidOperationException(
                        $"Source asset not found or cannot be read: {replacement.SourcePathId}");
                }

                AssetTypeValueField targetField = targetSession.Manager.GetBaseField(
                    targetSession.AssetsFileInstance,
                    replacement.TargetPathId);

                if (targetField.IsDummy)
                {
                    throw new InvalidOperationException(
                        $"Target asset not found or cannot be read: {replacement.TargetPathId}");
                }

                AssetFileInfo assetInfo = targetSession.AssetsFile.GetAssetInfo(replacement.TargetPathId);
                assetInfo.SetNewData(sourceField.Clone());
            }
        }
    }
}

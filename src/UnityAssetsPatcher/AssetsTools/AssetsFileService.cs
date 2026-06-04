using AssetsTools.NET;
using AssetsTools.NET.Extra;
using UnityAssetsPatcher.Core;

namespace UnityAssetsPatcher.AssetsTools;

public sealed class AssetsFileService : IAssetsFileService
{
    private readonly string _tpkFilePath;

    public AssetsFileService(string tpkFilePath)
    {
        _tpkFilePath = tpkFilePath;
    }

    public IReadOnlyList<AssetsInfo> ReadAssetsInfo(string assetsFilePath)
    {
        using AssetsFileSession session = AssetsFileSession.Open(assetsFilePath, _tpkFilePath);

        return session.AssetsFile.Metadata.AssetInfos
            .Select(info => new AssetsInfo(
                info.PathId,
                info.TypeId,
                GetTypeName(info.TypeId),
                info.ByteSize))
            .ToArray();
    }

    public AssetsFieldInfo ReadAssetsFieldInfo(string assetsFilePath, long pathId)
    {
        using AssetsFileSession session = AssetsFileSession.Open(assetsFilePath, _tpkFilePath);
        AssetTypeValueField field = session.Manager.GetBaseField(session.AssetsFileInstance, pathId);

        return field.IsDummy
            ? throw new InvalidOperationException($"Asset not found or cannot be read: {pathId}")
            : FieldTreeMapper.Map(field);
    }

    public void WritePatch(string inputPath, string outputPath, IReadOnlyList<PatchWriteAsset> plan)
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

                ApplyPatchPlan(session, plan);

                using FileStream outputStream = File.Create(tempPath);
                var writer = new AssetsFileWriter(outputStream);
                session.AssetsFile.Write(writer);
            }

            File.Move(tempPath, outputPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    public void WriteReplacements(string inputPath, string outputPath, IReadOnlyList<AssetReplacement> plan)
    {
        foreach (string sourceAssetsFilePath in plan
                     .Select(replacement => replacement.SourceAssetsFilePath)
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!File.Exists(sourceAssetsFilePath))
            {
                throw new FileNotFoundException($"Assets file not found: {sourceAssetsFilePath}",
                    sourceAssetsFilePath);
            }
        }

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

                ApplyReplacementPlan(session, plan);

                using FileStream outputStream = File.Create(tempPath);
                var writer = new AssetsFileWriter(outputStream);
                session.AssetsFile.Write(writer);
            }

            File.Move(tempPath, outputPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private static string GetTypeName(int typeId)
    {
        return Enum.IsDefined(typeof(AssetClassID), typeId) ? ((AssetClassID)typeId).ToString() : "Unknown";
    }

    private static string CreateTempPath(string outputPath, string? outputDirectory)
    {
        return Path.Combine(
            string.IsNullOrEmpty(outputDirectory) ? Directory.GetCurrentDirectory() : outputDirectory,
            $".{Path.GetFileName(outputPath)}.{Guid.NewGuid():N}.tmp");
    }

    private static void ApplyPatchPlan(AssetsFileSession session, IReadOnlyList<PatchWriteAsset> plan)
    {
        foreach (PatchWriteAsset asset in plan)
        {
            AssetTypeValueField baseField = session.Manager.GetBaseField(session.AssetsFileInstance, asset.PathId);

            if (baseField.IsDummy)
            {
                throw new InvalidOperationException($"Asset not found or cannot be read: {asset.PathId}");
            }

            AssetTypeValueField mutableField = baseField.Clone();

            foreach (PatchWriteOperation operation in asset.Operations)
            {
                AssetTypeValueField targetField = FieldLocator.Find(mutableField, operation.Path)
                                                  ?? throw new InvalidOperationException(
                                                      $"Field not found for Path ID {asset.PathId}: {operation.Path}");
                ValueWriter.WriteJsonValue(targetField, operation.To);
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

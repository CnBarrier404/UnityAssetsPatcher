using AssetsTools.NET;
using UnityAssetsPatcher.Abstractions.Assets;
using UnityAssetsPatcher.Abstractions.IO;
using UnityAssetsPatcher.Domain.Assets;

namespace UnityAssetsPatcher.AssetsTools;

public sealed class AssetsFileWriter : IAssetsFileWriter
{
    private readonly ClassPackageCache _classPackageCache;
    private readonly IFileOperations _fileOperations;
    private readonly IDirectoryOperations _directoryOperations;

    public AssetsFileWriter(
        Func<Stream> openTpkStream,
        IFileOperations fileOperations,
        IDirectoryOperations directoryOperations)
        : this(new ClassPackageCache(openTpkStream), fileOperations, directoryOperations) { }

    public AssetsFileWriter(
        ClassPackageCache classPackageCache,
        IFileOperations fileOperations,
        IDirectoryOperations directoryOperations)
    {
        ArgumentNullException.ThrowIfNull(classPackageCache);
        ArgumentNullException.ThrowIfNull(fileOperations);
        ArgumentNullException.ThrowIfNull(directoryOperations);

        _classPackageCache = classPackageCache;
        _fileOperations = fileOperations;
        _directoryOperations = directoryOperations;
    }

    public void WriteFieldPatches(string inputPath, string outputPath, IReadOnlyList<AssetFieldPatch> plan)
    {
        WriteAssetsFile(inputPath, outputPath, session => ApplyPatchPlan(session, plan));
    }

    public void WriteReplacements(string inputPath, string outputPath, IReadOnlyList<AssetReplacement> plan)
    {
        ValidateReplacementSources(plan);
        WriteAssetsFile(inputPath, outputPath, session => ApplyReplacementPlan(session, plan));
    }

    public void WriteFieldPatchesAndCopies(
        string inputPath,
        string outputPath,
        IReadOnlyList<AssetFieldPatch> fieldPatches,
        IReadOnlyList<AssetCopy> copies)
    {
        WriteAssetsFile(inputPath, outputPath, session => ApplyFieldPatchesAndCopies(session, fieldPatches, copies));
    }

    private void WriteAssetsFile(string inputPath, string outputPath, Action<AssetsFileSession> applyChanges)
    {
        string? outputDirectory = Path.GetDirectoryName(outputPath);

        if (!string.IsNullOrEmpty(outputDirectory))
        {
            _directoryOperations.Create(outputDirectory);
        }

        _fileOperations.Write(outputPath, outputStream =>
        {
            using AssetsFileSession session = AssetsFileSession.Open(inputPath, _classPackageCache);

            applyChanges.Invoke(session);

            WriteSessionToStream(session, outputStream);
        });
    }

    private static void ValidateReplacementSources(IReadOnlyList<AssetReplacement> plan)
    {
        foreach (string sourceAssetsFilePath in plan
                     .Select(replacement => replacement.SourceAssetsFilePath)
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!File.Exists(sourceAssetsFilePath))
            {
                throw new FileNotFoundException($"Assets file not found: {sourceAssetsFilePath}", sourceAssetsFilePath);
            }
        }
    }

    private static void WriteSessionToStream(AssetsFileSession session, Stream outputStream)
    {
        session.WriteTo(outputStream);
    }

    private static void ApplyPatchPlan(AssetsFileSession session, IReadOnlyList<AssetFieldPatch> plan)
    {
        var mutableFields = new Dictionary<long, AssetTypeValueField>();
        ApplyFieldPatches(session, mutableFields, plan);
        SetAssetData(session, mutableFields);
    }

    private static void ApplyFieldPatchesAndCopies(
        AssetsFileSession session,
        IReadOnlyList<AssetFieldPatch> fieldPatches,
        IReadOnlyList<AssetCopy> copies)
    {
        var mutableFields = new Dictionary<long, AssetTypeValueField>();

        ApplyFieldPatches(session, mutableFields, fieldPatches);

        foreach (AssetCopy copy in copies)
        {
            AssetTypeValueField sourceField = GetMutableField(session, mutableFields, copy.SourcePathId);
            AssetTypeValueField currentTarget = GetMutableField(session, mutableFields, copy.TargetPathId);
            AssetTypeValueField copiedField = sourceField.Clone();
            PreserveName(currentTarget, copiedField, copy.TargetPathId);
            mutableFields[copy.TargetPathId] = copiedField;
        }

        SetAssetData(session, mutableFields);
    }

    private static void ApplyFieldPatches(
        AssetsFileSession session,
        IDictionary<long, AssetTypeValueField> mutableFields,
        IReadOnlyList<AssetFieldPatch> fieldPatches)
    {
        foreach (AssetFieldPatch asset in fieldPatches)
        {
            AssetTypeValueField mutableField = GetMutableField(session, mutableFields, asset.PathId);

            foreach (FieldPatchOperation operation in asset.Operations)
            {
                AssetTypeValueField targetField = AssetFieldLocator.Find(mutableField, operation.Path)
                                                  ?? throw new InvalidOperationException(
                                                      $"Field not found for Path ID {asset.PathId}: {operation.Path}");
                AssetFieldWriter.WriteJsonValue(targetField, operation.To);
            }
        }
    }

    private static void SetAssetData(AssetsFileSession session,
        IEnumerable<KeyValuePair<long, AssetTypeValueField>> mutableFields)
    {
        foreach ((long pathId, AssetTypeValueField field) in mutableFields)
        {
            session.SetData(pathId, field);
        }
    }

    private static AssetTypeValueField GetMutableField(
        AssetsFileSession session,
        IDictionary<long, AssetTypeValueField> mutableFields,
        long pathId)
    {
        if (mutableFields.TryGetValue(pathId, out AssetTypeValueField? mutableField))
        {
            return mutableField;
        }

        AssetTypeValueField baseField = session.GetBaseField(pathId);

        if (baseField.IsDummy)
        {
            throw new InvalidOperationException($"Asset not found or cannot be read: {pathId}");
        }

        mutableField = baseField.Clone();
        mutableFields.Add(pathId, mutableField);

        return mutableField;
    }

    private static void PreserveName(
        AssetTypeValueField currentTarget,
        AssetTypeValueField copiedField,
        long targetPathId)
    {
        AssetTypeValueField? currentName = AssetFieldLocator.Find(currentTarget, "m_Name");
        AssetTypeValueField? copiedName = AssetFieldLocator.Find(copiedField, "m_Name");

        if (currentName?.Value?.ValueType != AssetValueType.String ||
            copiedName?.Value?.ValueType != AssetValueType.String)
        {
            throw new InvalidOperationException(
                $"Copy asset target Path ID {targetPathId} does not have a scalar string 'm_Name' field to preserve.");
        }

        copiedName.AsString = currentName.AsString;
    }

    private void ApplyReplacementPlan(AssetsFileSession targetSession, IReadOnlyList<AssetReplacement> plan)
    {
        foreach (var sourceGroup in plan.GroupBy(replacement => replacement.SourceAssetsFilePath,
                     StringComparer.OrdinalIgnoreCase))
        {
            using AssetsFileSession sourceSession = AssetsFileSession.Open(sourceGroup.Key, _classPackageCache);

            foreach (AssetReplacement replacement in sourceGroup)
            {
                AssetTypeValueField sourceField = sourceSession.GetBaseField(replacement.SourcePathId);

                if (sourceField.IsDummy)
                {
                    throw new InvalidOperationException(
                        $"Source asset not found or cannot be read: {replacement.SourcePathId}");
                }

                AssetTypeValueField targetField = targetSession.GetBaseField(replacement.TargetPathId);

                if (targetField.IsDummy)
                {
                    throw new InvalidOperationException(
                        $"Target asset not found or cannot be read: {replacement.TargetPathId}");
                }

                targetSession.SetData(replacement.TargetPathId, sourceField.Clone());
            }
        }
    }
}

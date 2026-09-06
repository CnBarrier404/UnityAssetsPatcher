using AssetsTools.NET;
using AssetsTools.NET.Extra;
using Microsoft.Extensions.Logging;
using UnityAssetsPatcher.Application.Assets;
using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Domain.Assets;

namespace UnityAssetsPatcher.Infrastructure.AssetsTools;

internal sealed class AssetsToolsAssetFileSession : IAssetFileSession
{
    private static readonly IReadOnlyDictionary<int, string> TypeNames = Enum
        .GetValues<AssetClassID>()
        .Distinct()
        .ToDictionary(type => (int)type, type => Enum.GetName(type) ?? "Unknown");

    private readonly ClassPackageCache _classPackageCache;
    private readonly IFileSystemOperations _fileSystemOperations;
    private readonly string _inputPath;
    private readonly ILogger<AssetsToolsAssetFileSession> _logger;
    private readonly AssetsFileSession _session;

    private readonly Dictionary<string, AssetsFileSession> _pendingSourceSessions =
        new(StringComparer.OrdinalIgnoreCase);

    private IReadOnlyList<AssetInfo>? _assets;
    private Exception? _lastOperationException;
    private bool _disposed;
    private bool _sessionDisposed;

    public AssetsToolsAssetFileSession(
        string inputPath,
        ClassPackageCache classPackageCache,
        IFileSystemOperations fileSystemOperations,
        ILogger<AssetsToolsAssetFileSession> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);
        ArgumentNullException.ThrowIfNull(classPackageCache);
        ArgumentNullException.ThrowIfNull(fileSystemOperations);
        ArgumentNullException.ThrowIfNull(logger);

        _classPackageCache = classPackageCache;
        _fileSystemOperations = fileSystemOperations;
        _inputPath = inputPath;
        _logger = logger;
        _session = AssetsFileSession.Open(inputPath, classPackageCache);
        AssetsToolsLog.AssetsFileOpened(_logger, _inputPath);
    }

    public IReadOnlyList<AssetInfo> ReadAssets()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        return _assets ??= ReadSessionAssets();
    }

    public AssetField ReadField(AssetPathId pathId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        AssetsToolsLog.ReadingAssetField(_logger, pathId.Value, _inputPath);
        AssetTypeValueField field = GetRequiredField(_session, pathId);

        return AssetFieldMapper.Map(field);
    }

    public void Write(string outputPath, AssetMutationPlan plan)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentNullException.ThrowIfNull(plan);

        _disposed = true;
        _assets = null;

        Exception? primaryException = null;

        try
        {
            AssetsToolsLog.WritingAssetsFile(
                _logger,
                plan.Mutations.Count,
                _inputPath,
                outputPath);
            ValidateReplacementSources(plan.Mutations);
            WriteOutput(outputPath, plan.Mutations);
            AssetsToolsLog.AssetsFileWritten(
                _logger,
                plan.Mutations.Count,
                outputPath);
        }
        catch (Exception exception)
        {
            primaryException = exception;
        }

        var cleanupExceptions = ResourceCleanup.RunAll(
        [
            DisposeSessions
        ]);

        try
        {
            ResourceCleanup.ThrowIfFailed(primaryException, cleanupExceptions);
        }
        catch (Exception exception)
        {
            _lastOperationException = exception;
            throw;
        }
    }

    public void Dispose()
    {
        _disposed = true;
        _assets = null;

        var cleanupExceptions = ResourceCleanup.RunAll(
        [
            DisposeSessions
        ]);

        if (cleanupExceptions.Count > 0)
        {
            ResourceCleanup.ThrowIfFailed(_lastOperationException, cleanupExceptions);
        }
    }

    private IReadOnlyList<AssetInfo> ReadSessionAssets()
    {
        IReadOnlyList<AssetInfo> assets =
        [
            .. _session.AssetsFile.Metadata.AssetInfos
                .Select(info => new AssetInfo(new AssetPathId(info.PathId), GetTypeName(info.TypeId)))
        ];

        AssetsToolsLog.AssetsRead(
            _logger,
            assets.Count,
            _inputPath);

        return assets;
    }

    private void WriteOutput(string outputPath, IReadOnlyList<AssetMutation> mutations)
    {
        string? outputDirectory = Path.GetDirectoryName(outputPath);

        if (!string.IsNullOrEmpty(outputDirectory))
        {
            _fileSystemOperations.EnsureDirectory(outputDirectory);
        }

        _fileSystemOperations.WriteFileAtomically(
            outputPath,
            FileDestinationMode.CreateOrReplace,
            outputStream =>
            {
                ApplyMutationPlan(mutations);

                _session.WriteTo(outputStream);
            });
    }

    private void DisposeSessions()
    {
        var cleanupExceptions = ResourceCleanup.RunAll(
        [
            DisposeTargetSession,
            .. _pendingSourceSessions
                .ToArray()
                .Select(source => (Action)(() =>
                {
                    source.Value.Dispose();
                    _pendingSourceSessions.Remove(source.Key);
                    AssetsToolsLog.AssetsFileClosed(_logger, source.Key);
                }))
        ]);

        ResourceCleanup.ThrowIfFailed(null, cleanupExceptions);
    }

    private void DisposeTargetSession()
    {
        if (_sessionDisposed)
        {
            return;
        }

        _session.Dispose();
        _sessionDisposed = true;
        AssetsToolsLog.AssetsFileClosed(_logger, _inputPath);
    }

    private static void ValidateReplacementSources(IReadOnlyList<AssetMutation> mutations)
    {
        foreach (string sourcePath in mutations
                     .OfType<ReplaceAsset>()
                     .Select(replacement => replacement.Source.AssetsFilePath)
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!File.Exists(sourcePath))
            {
                throw new FileNotFoundException($"Assets file not found: {sourcePath}", sourcePath);
            }
        }
    }

    private void ApplyMutationPlan(IReadOnlyList<AssetMutation> mutations)
    {
        var mutableFields = new Dictionary<AssetPathId, AssetTypeValueField>();
        var fieldLocators = new Dictionary<AssetPathId, AssetFieldLocator>();
        var sourceSessions = new Dictionary<string, AssetsFileSession>(StringComparer.OrdinalIgnoreCase);
        Exception? primaryException = null;

        try
        {
            foreach (AssetMutation mutation in mutations)
            {
                switch (mutation)
                {
                    case PatchAssetFields patch:
                        ApplyFieldPatch(mutableFields, fieldLocators, patch);
                        break;
                    case CopyAsset copy:
                        ApplyCopy(mutableFields, fieldLocators, copy);
                        break;
                    case ReplaceAsset replacement:
                        ApplyReplacement(mutableFields, fieldLocators, sourceSessions, replacement);
                        break;
                    default:
                        throw new InvalidOperationException(
                            $"Unsupported asset mutation: {mutation.GetType().Name}.");
                }
            }

            foreach ((AssetPathId pathId, AssetTypeValueField field) in mutableFields)
            {
                _session.SetData(pathId, field);
            }
        }
        catch (Exception exception)
        {
            primaryException = exception;
        }

        var cleanupExceptions = ResourceCleanup.RunAll(
            sourceSessions
                .ToArray()
                .Select(source => (Action)(() =>
                {
                    source.Value.Dispose();
                    sourceSessions.Remove(source.Key);
                    AssetsToolsLog.AssetsFileClosed(_logger, source.Key);
                })));

        foreach ((string sourcePath, AssetsFileSession sourceSession) in sourceSessions)
        {
            _pendingSourceSessions[sourcePath] = sourceSession;
        }

        ResourceCleanup.ThrowIfFailed(primaryException, cleanupExceptions);
    }

    private void ApplyFieldPatch(
        Dictionary<AssetPathId, AssetTypeValueField> mutableFields,
        Dictionary<AssetPathId, AssetFieldLocator> fieldLocators,
        PatchAssetFields patch)
    {
        AssetTypeValueField mutableField = GetMutableField(mutableFields, patch.Asset);
        AssetFieldLocator fieldLocator = GetFieldLocator(fieldLocators, patch.Asset, mutableField);

        foreach (SetAssetField assignment in patch.Assignments)
        {
            AssetTypeValueField targetField = fieldLocator.Find(assignment.Path)
                                              ?? throw new InvalidOperationException(
                                                  $"Field not found for Path ID {patch.Asset}: {assignment.Path}");
            bool structureChanged = AssetFieldWriter.Write(targetField, assignment.Value);

            if (structureChanged)
            {
                fieldLocator.InvalidateStructure();
            }
        }
    }

    private void ApplyCopy(
        Dictionary<AssetPathId, AssetTypeValueField> mutableFields,
        Dictionary<AssetPathId, AssetFieldLocator> fieldLocators,
        CopyAsset copy)
    {
        AssetTypeValueField sourceField = GetMutableField(mutableFields, copy.Source);
        AssetTypeValueField currentTarget = GetMutableField(mutableFields, copy.Target);
        AssetTypeValueField copiedField = sourceField.Clone();

        PreserveName(currentTarget, copiedField, copy.Target);
        mutableFields[copy.Target] = copiedField;
        fieldLocators.Remove(copy.Target);
    }

    private void ApplyReplacement(
        Dictionary<AssetPathId, AssetTypeValueField> mutableFields,
        Dictionary<AssetPathId, AssetFieldLocator> fieldLocators,
        IDictionary<string, AssetsFileSession> sourceSessions,
        ReplaceAsset replacement)
    {
        AssetsFileSession sourceSession = GetSourceSession(sourceSessions, replacement.Source.AssetsFilePath);
        AssetReplacementCompatibilityValidator.ValidateMetadataAndAssetCompatibility(
            _session,
            replacement.Target,
            sourceSession,
            replacement.Source.Asset);

        AssetTypeValueField sourceField = GetRequiredField(sourceSession, replacement.Source.Asset);
        AssetTypeValueField targetField = GetMutableField(mutableFields, replacement.Target);

        AssetReplacementCompatibilityValidator.ValidateFields(
            _session,
            replacement.Target,
            targetField,
            sourceSession,
            replacement.Source.Asset,
            sourceField);

        mutableFields[replacement.Target] = sourceField.Clone();
        fieldLocators.Remove(replacement.Target);
    }

    private AssetsFileSession GetSourceSession(
        IDictionary<string, AssetsFileSession> sourceSessions,
        string sourcePath)
    {
        string fullPath = Path.GetFullPath(sourcePath);

        if (sourceSessions.TryGetValue(fullPath, out AssetsFileSession? sourceSession))
        {
            return sourceSession;
        }

        AssetsToolsLog.OpeningAssetsFile(_logger, fullPath);

        sourceSession = AssetsFileSession.Open(fullPath, _classPackageCache);

        AssetsToolsLog.AssetsFileOpened(_logger, fullPath);

        sourceSessions.Add(fullPath, sourceSession);

        return sourceSession;
    }

    private AssetTypeValueField GetMutableField(
        IDictionary<AssetPathId, AssetTypeValueField> mutableFields,
        AssetPathId pathId)
    {
        if (mutableFields.TryGetValue(pathId, out AssetTypeValueField? mutableField))
        {
            return mutableField;
        }

        mutableField = GetRequiredField(_session, pathId).Clone();
        mutableFields.Add(pathId, mutableField);

        return mutableField;
    }

    private static AssetFieldLocator GetFieldLocator(
        IDictionary<AssetPathId, AssetFieldLocator> fieldLocators,
        AssetPathId pathId,
        AssetTypeValueField mutableField)
    {
        if (fieldLocators.TryGetValue(pathId, out AssetFieldLocator? fieldLocator))
        {
            return fieldLocator;
        }

        fieldLocator = new AssetFieldLocator(mutableField);
        fieldLocators.Add(pathId, fieldLocator);

        return fieldLocator;
    }

    private static AssetTypeValueField GetRequiredField(AssetsFileSession session, AssetPathId pathId)
    {
        if (!session.ContainsAsset(pathId))
        {
            throw new InvalidOperationException($"Asset not found or cannot be read: {pathId}");
        }

        AssetTypeValueField field = session.GetBaseField(pathId);

        return field.IsDummy
            ? throw new InvalidOperationException($"Asset not found or cannot be read: {pathId}")
            : field;
    }

    private static void PreserveName(
        AssetTypeValueField currentTarget,
        AssetTypeValueField copiedField,
        AssetPathId targetPathId)
    {
        AssetTypeValueField currentName = currentTarget["m_Name"];
        AssetTypeValueField copiedName = copiedField["m_Name"];

        if (currentName.Value?.ValueType != AssetValueType.String ||
            copiedName.Value?.ValueType != AssetValueType.String)
        {
            throw new InvalidOperationException(
                $"Copy asset target Path ID {targetPathId} does not have a scalar string 'm_Name' field to preserve.");
        }

        copiedName.AsString = currentName.AsString;
    }

    private static string GetTypeName(int typeId)
    {
        return TypeNames.GetValueOrDefault(typeId, "Unknown");
    }
}

using System.Text.Json;
using UnityAssetsPatcher.Application.Assets;
using UnityAssetsPatcher.Domain.Assets;

namespace UnityAssetsPatcher.Infrastructure.AssetsTools;

public sealed class AssetFileAccessScopeFactory : IAssetsAccessScopeFactory
{
    private readonly IAssetFileSessionFactory _sessionFactory;

    public AssetFileAccessScopeFactory(IAssetFileSessionFactory sessionFactory)
    {
        ArgumentNullException.ThrowIfNull(sessionFactory);

        _sessionFactory = sessionFactory;
    }

    public IAssetsAccessScope CreateScope()
    {
        return new AssetFileAccessScope(_sessionFactory);
    }
}

internal sealed class AssetFileAccessScope : IAssetsAccessScope, IAssetsFileReader, IAssetsFileWriter
{
    public IAssetsFileReader Reader => this;

    public IAssetsFileWriter Writer => this;

    private readonly Dictionary<string, IAssetFileSession> _readSessions = new(TrustedPathComparer.Instance);

    private readonly IAssetFileSessionFactory _sessionFactory;
    private AssetField? _cachedReadField;
    private string? _cachedReadFieldPath;
    private AssetPathId _cachedReadFieldPathId;
    private Exception? _lastOperationException;
    private bool _disposed;

    public AssetFileAccessScope(IAssetFileSessionFactory sessionFactory)
    {
        ArgumentNullException.ThrowIfNull(sessionFactory);

        _sessionFactory = sessionFactory;
    }

    public IReadOnlyList<AssetInfo> ReadAssets(string assetsFilePath)
    {
        return GetReadSession(assetsFilePath).ReadAssets();
    }

    public AssetField ReadField(string assetsFilePath, long pathId)
    {
        string fullPath = GetFullPath(assetsFilePath);
        IAssetFileSession session = GetReadSession(fullPath);
        AssetPathId assetPathId = new(pathId);

        if (_cachedReadField is not null &&
            _cachedReadFieldPathId == assetPathId &&
            TrustedPathComparer.Instance.Equals(_cachedReadFieldPath, fullPath))
        {
            return _cachedReadField;
        }

        AssetField fieldTree = session.ReadField(assetPathId);
        _cachedReadField = fieldTree;
        _cachedReadFieldPath = fullPath;
        _cachedReadFieldPathId = assetPathId;

        return fieldTree;
    }

    public void WriteFieldPatches(string inputPath, string outputPath, IReadOnlyList<AssetFieldPatch> plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        Write(inputPath, outputPath, session => MapFieldPatches(session, plan));
    }

    public void WriteReplacements(string inputPath, string outputPath, IReadOnlyList<AssetReplacement> plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        Write(
            inputPath,
            outputPath,
            _ =>
            [
                .. plan.Select(replacement => new ReplaceAsset(
                    new AssetSource(replacement.SourceAssetsFilePath, replacement.SourcePathId),
                    replacement.TargetPathId))
            ]);
    }

    public void WriteFieldPatchesAndCopies(
        string inputPath,
        string outputPath,
        IReadOnlyList<AssetFieldPatch> fieldPatches,
        IReadOnlyList<AssetCopy> copies)
    {
        ArgumentNullException.ThrowIfNull(fieldPatches);
        ArgumentNullException.ThrowIfNull(copies);

        Write(
            inputPath,
            outputPath,
            session =>
            [
                .. MapFieldPatches(session, fieldPatches),
                .. copies.Select(copy => new CopyAsset(copy.SourcePathId, copy.TargetPathId))
            ]);
    }

    public void Dispose()
    {
        if (_disposed && _readSessions.Count == 0)
        {
            return;
        }

        _disposed = true;
        var cleanupExceptions = CloseReadSessions();

        if (cleanupExceptions.Count > 0)
        {
            ResourceCleanup.ThrowIfFailed(_lastOperationException, cleanupExceptions);
        }
    }

    private IAssetFileSession GetReadSession(string assetsFilePath)
    {
        string fullPath = GetFullPath(assetsFilePath);

        if (_readSessions.TryGetValue(fullPath, out IAssetFileSession? session))
        {
            return session;
        }

        session = _sessionFactory.Open(fullPath);
        _readSessions.Add(fullPath, session);

        return session;
    }

    private void Write(string inputPath, string outputPath, Func<IAssetFileSession, AssetMutation[]> createMutations)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentNullException.ThrowIfNull(createMutations);

        try
        {
            ResourceCleanup.ThrowIfFailed(null, CloseReadSessions());
        }
        catch (Exception exception)
        {
            _lastOperationException = exception;
            _disposed = true;

            throw;
        }

        IAssetFileSession session = _sessionFactory.Open(inputPath);
        Exception? primaryException = null;

        try
        {
            var mutations = createMutations(session);
            session.Write(outputPath, new AssetMutationPlan(mutations));
        }
        catch (Exception exception)
        {
            primaryException = exception;
        }

        var cleanupExceptions = ResourceCleanup.RunAll(
        [
            session.Dispose
        ]);

        if (cleanupExceptions.Count > 0)
        {
            _readSessions[Path.GetFullPath(inputPath)] = session;
            _disposed = true;
        }

        try
        {
            ResourceCleanup.ThrowIfFailed(primaryException, cleanupExceptions);
        }
        catch (Exception exception)
        {
            if (cleanupExceptions.Count > 0)
            {
                _lastOperationException = exception;
            }

            throw;
        }
    }

    private static AssetMutation[] MapFieldPatches(
        IAssetFileSession session,
        IReadOnlyList<AssetFieldPatch> fieldPatches)
    {
        return
        [
            .. fieldPatches.Select(patch => MapFieldPatch(session, patch))
        ];
    }

    private static PatchAssetFields MapFieldPatch(
        IAssetFileSession session,
        AssetFieldPatch patch)
    {
        AssetField fieldTree = session.ReadField(patch.PathId);

        return new PatchAssetFields(
            patch.PathId,
            patch.Operations.Select(operation => MapFieldAssignment(fieldTree, patch.PathId, operation)));
    }

    private static SetAssetField MapFieldAssignment(
        AssetField root,
        AssetPathId pathId,
        FieldPatchOperation operation)
    {
        var path = new AssetFieldPath(operation.Path);
        var resolver = new AssetFieldPathResolver<AssetField>(
            root,
            field => field.Name,
            field => field.Children,
            field => field.Value?.ToInvariantString());
        AssetField target = resolver.Find(path) ??
                            throw new InvalidOperationException(
                                $"Field not found for Path ID {pathId}: {operation.Path}");
        AssetWriteValue value = ConvertWriteValue(target, operation.To);

        return new SetAssetField(path, value);
    }

    private static AssetWriteValue ConvertWriteValue(AssetField target, JsonElement value)
    {
        return target switch
        {
            AssetScalarField scalar => new AssetScalarWriteValue(ConvertScalarValue(scalar.Value.Kind, value)),
            AssetArrayField { ElementSchema: AssetScalarFieldSchema element } =>
                new AssetScalarArrayWriteValue(
                    element.Kind,
                    value.EnumerateArray().Select(item => ConvertScalarValue(element.Kind, item))),
            _ => throw new InvalidOperationException(
                $"Field '{target.Name}' of type '{target.TypeName}' cannot be written as a scalar or scalar array.")
        };
    }

    private static AssetScalarValue ConvertScalarValue(AssetScalarKind kind, JsonElement value)
    {
        return kind switch
        {
            AssetScalarKind.Boolean => new AssetScalarValue.Boolean(value.GetBoolean()),
            AssetScalarKind.Int8 => new AssetScalarValue.Int8(value.GetSByte()),
            AssetScalarKind.UInt8 => new AssetScalarValue.UInt8(value.GetByte()),
            AssetScalarKind.Int16 => new AssetScalarValue.Int16(value.GetInt16()),
            AssetScalarKind.UInt16 => new AssetScalarValue.UInt16(value.GetUInt16()),
            AssetScalarKind.Int32 => new AssetScalarValue.Int32(value.GetInt32()),
            AssetScalarKind.UInt32 => new AssetScalarValue.UInt32(value.GetUInt32()),
            AssetScalarKind.Int64 => new AssetScalarValue.Int64(value.GetInt64()),
            AssetScalarKind.UInt64 => new AssetScalarValue.UInt64(value.GetUInt64()),
            AssetScalarKind.Float => new AssetScalarValue.Float(value.GetSingle()),
            AssetScalarKind.Double => new AssetScalarValue.Double(value.GetDouble()),
            AssetScalarKind.String => value.ValueKind == JsonValueKind.String
                ? new AssetScalarValue.String(value.GetString()!)
                : throw new InvalidOperationException(
                    $"Cannot write JSON value of kind '{value.ValueKind}' to an asset string field."),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported asset scalar kind.")
        };
    }

    private IReadOnlyList<Exception> CloseReadSessions()
    {
        var cleanupExceptions = ResourceCleanup.RunAll(
            _readSessions
                .ToArray()
                .Select(entry => (Action)(() =>
                {
                    entry.Value.Dispose();
                    _readSessions.Remove(entry.Key);
                })));

        _cachedReadField = null;
        _cachedReadFieldPath = null;
        _cachedReadFieldPathId = default;

        return cleanupExceptions;
    }

    private string GetFullPath(string assetsFilePath)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(assetsFilePath);

        return Path.GetFullPath(assetsFilePath);
    }

    private sealed class TrustedPathComparer : IEqualityComparer<string>
    {
        public static TrustedPathComparer Instance { get; } = new();

        public bool Equals(string? left, string? right)
        {
            return string.Equals(
                left,
                right,
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        }

        public int GetHashCode(string path)
        {
            return (OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal)
                .GetHashCode(path);
        }
    }
}

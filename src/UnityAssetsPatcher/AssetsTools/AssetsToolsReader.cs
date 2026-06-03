using System.Text.Json;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using UnityAssetsPatcher.Core;

namespace UnityAssetsPatcher.AssetsTools;

public sealed class AssetsToolsReader : IAssetsReader, IAssetsPatchWriter
{
    private readonly string _tpkFilePath;

    public AssetsToolsReader(string tpkFilePath)
    {
        _tpkFilePath = tpkFilePath;
    }

    public IReadOnlyList<AssetsInfo> ReadAssetsInfo(string assetsFilePath)
    {
        if (!File.Exists(assetsFilePath))
        {
            throw new FileNotFoundException($"Assets file not found: {assetsFilePath}", assetsFilePath);
        }

        if (!File.Exists(_tpkFilePath))
        {
            throw new FileNotFoundException($"TPK file not found: {_tpkFilePath}", _tpkFilePath);
        }

        var manager = new AssetsManager();

        try
        {
            manager.LoadClassPackage(_tpkFilePath);
            AssetsFileInstance assetsFileInstance = manager.LoadAssetsFile(assetsFilePath, true);
            AssetsFile assetsFile = assetsFileInstance.file;

            // 不同 Unity 版本的序列化字段布局可能不同，必须按文件声明的版本选择类型数据库
            manager.LoadClassDatabaseFromPackage(assetsFile.Metadata.UnityVersion);

            return assetsFile.Metadata.AssetInfos
                .Select(info => new AssetsInfo(
                    info.PathId,
                    info.TypeId,
                    GetTypeName(info.TypeId),
                    info.ByteSize))
                .ToArray();
        }
        finally
        {
            // AssetsManager 会持有文件流；即使解析失败也必须释放，否则后续无法替换或恢复原文件
            manager.UnloadAll(true);
        }
    }

    public AssetsFieldInfo ReadAssetsFieldInfo(string assetsFilePath, long pathId)
    {
        if (!File.Exists(assetsFilePath))
        {
            throw new FileNotFoundException($"Assets file not found: {assetsFilePath}", assetsFilePath);
        }

        if (!File.Exists(_tpkFilePath))
        {
            throw new FileNotFoundException($"TPK file not found: {_tpkFilePath}", _tpkFilePath);
        }

        var manager = new AssetsManager();

        try
        {
            manager.LoadClassPackage(_tpkFilePath);
            AssetsFileInstance assetsFileInstance = manager.LoadAssetsFile(assetsFilePath, true);

            manager.LoadClassDatabaseFromPackage(assetsFileInstance.file.Metadata.UnityVersion);

            AssetTypeValueField field = manager.GetBaseField(assetsFileInstance, pathId);

            return field.IsDummy
                ? throw new InvalidOperationException($"Asset not found or cannot be read: {pathId}")
                : CreateAssetsFieldInfo(field);
        }
        finally
        {
            manager.UnloadAll(true);
        }
    }

    public void WritePatch(string inputPath, string outputPath, IReadOnlyList<PatchWriteAsset> plan)
    {
        if (!File.Exists(inputPath))
        {
            throw new FileNotFoundException($"Assets file not found: {inputPath}", inputPath);
        }

        if (!File.Exists(_tpkFilePath))
        {
            throw new FileNotFoundException($"TPK file not found: {_tpkFilePath}", _tpkFilePath);
        }

        string? outputDirectory = Path.GetDirectoryName(outputPath);

        if (!string.IsNullOrEmpty(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        string tempPath = Path.Combine(
            string.IsNullOrEmpty(outputDirectory) ? Directory.GetCurrentDirectory() : outputDirectory,
            $".{Path.GetFileName(outputPath)}.{Guid.NewGuid():N}.tmp");
        var manager = new AssetsManager();

        try
        {
            try
            {
                manager.LoadClassPackage(_tpkFilePath);
                AssetsFileInstance assetsFileInstance = manager.LoadAssetsFile(inputPath, true);
                AssetsFile assetsFile = assetsFileInstance.file;

                manager.LoadClassDatabaseFromPackage(assetsFile.Metadata.UnityVersion);

                foreach (PatchWriteAsset asset in plan)
                {
                    AssetTypeValueField baseField = manager.GetBaseField(assetsFileInstance, asset.PathId);

                    if (baseField.IsDummy)
                    {
                        throw new InvalidOperationException($"Asset not found or cannot be read: {asset.PathId}");
                    }

                    AssetTypeValueField mutableField = baseField.Clone();

                    foreach (PatchWriteOperation operation in asset.Operations)
                    {
                        AssetTypeValueField targetField = FindAssetToolsField(mutableField, operation.Path)
                                                          ?? throw new InvalidOperationException(
                                                              $"Field not found for Path ID {asset.PathId}: {operation.Path}");
                        ApplyJsonValue(targetField, operation.To);
                    }

                    AssetFileInfo assetInfo = assetsFile.GetAssetInfo(asset.PathId);
                    assetInfo.SetNewData(mutableField);
                }

                using FileStream outputStream = File.Create(tempPath);
                var writer = new AssetsFileWriter(outputStream);
                assetsFile.Write(writer);
            }
            finally
            {
                manager.UnloadAll(true);
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

    private static AssetsFieldInfo CreateAssetsFieldInfo(AssetTypeValueField field)
    {
        return new AssetsFieldInfo(
            field.FieldName,
            field.TypeName,
            field.Value?.ToString(),
            field.Children.Select(CreateAssetsFieldInfo).ToArray());
    }

    private static string GetTypeName(int typeId)
    {
        return Enum.IsDefined(typeof(AssetClassID), typeId) ? ((AssetClassID)typeId).ToString() : "Unknown";
    }

    private static AssetTypeValueField? FindAssetToolsField(AssetTypeValueField field, string path)
    {
        var segments = AssetFieldPath.Parse(path);

        if (segments is [{ HasSelector: false }])
        {
            return FindAssetToolsDescendantByName(field, segments[0].Name);
        }

        AssetTypeValueField? current = field;

        foreach (AssetFieldPathSegment segment in segments)
        {
            current = FindAssetToolsChildBySegment(current, segment);

            if (current is null)
            {
                return null;
            }
        }

        return current;
    }

    private static AssetTypeValueField? FindAssetToolsDescendantByName(AssetTypeValueField field, string name)
    {
        if (string.Equals(field.FieldName, name, StringComparison.Ordinal))
        {
            return field;
        }

        return field.Children
            .Select(child => FindAssetToolsDescendantByName(child, name))
            .OfType<AssetTypeValueField>()
            .FirstOrDefault();
    }

    private static AssetTypeValueField? FindAssetToolsChildBySegment(
        AssetTypeValueField field,
        AssetFieldPathSegment segment)
    {
        return field.Children.FirstOrDefault(child =>
            string.Equals(child.FieldName, segment.Name, StringComparison.Ordinal) &&
            MatchesAssetToolsSelector(child, segment));
    }

    private static bool MatchesAssetToolsSelector(AssetTypeValueField field, AssetFieldPathSegment segment)
    {
        if (!segment.HasSelector)
        {
            return true;
        }

        AssetTypeValueField? selectorField = field.Children.FirstOrDefault(child =>
            string.Equals(child.FieldName, segment.SelectorFieldName, StringComparison.Ordinal));

        return string.Equals(selectorField?.Value?.ToString(), segment.SelectorValue, StringComparison.Ordinal);
    }

    private static void ApplyJsonValue(AssetTypeValueField field, JsonElement value)
    {
        if (field.Value is null)
        {
            throw new InvalidOperationException($"Field '{field.FieldName}' is not a scalar value.");
        }

        switch (field.Value.ValueType)
        {
            case AssetValueType.Bool:
                field.AsBool = value.ValueKind switch
                {
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    _ => throw CreateTypeMismatch(field, value)
                };
                break;
            case AssetValueType.Int8:
                field.AsSByte = checked((sbyte)GetInt64(value, field));
                break;
            case AssetValueType.UInt8:
                field.AsByte = checked((byte)GetUInt64(value, field));
                break;
            case AssetValueType.Int16:
                field.AsShort = checked((short)GetInt64(value, field));
                break;
            case AssetValueType.UInt16:
                field.AsUShort = checked((ushort)GetUInt64(value, field));
                break;
            case AssetValueType.Int32:
                field.AsInt = checked((int)GetInt64(value, field));
                break;
            case AssetValueType.UInt32:
                field.AsUInt = checked((uint)GetUInt64(value, field));
                break;
            case AssetValueType.Int64:
                field.AsLong = GetInt64(value, field);
                break;
            case AssetValueType.UInt64:
                field.AsULong = GetUInt64(value, field);
                break;
            case AssetValueType.Float:
                field.AsFloat = (float)GetDouble(value, field);
                break;
            case AssetValueType.Double:
                field.AsDouble = GetDouble(value, field);
                break;
            case AssetValueType.String:
                field.AsString = value.ValueKind == JsonValueKind.String
                    ? value.GetString() ?? string.Empty
                    : throw CreateTypeMismatch(field, value);
                break;
            default:
                throw new InvalidOperationException(
                    $"Field '{field.FieldName}' has unsupported value type: {field.Value.ValueType}.");
        }
    }

    private static long GetInt64(JsonElement value, AssetTypeValueField field)
    {
        return value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out long result)
            ? result
            : throw CreateTypeMismatch(field, value);
    }

    private static ulong GetUInt64(JsonElement value, AssetTypeValueField field)
    {
        return value.ValueKind == JsonValueKind.Number && value.TryGetUInt64(out ulong result)
            ? result
            : throw CreateTypeMismatch(field, value);
    }

    private static double GetDouble(JsonElement value, AssetTypeValueField field)
    {
        return value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out double result)
            ? result
            : throw CreateTypeMismatch(field, value);
    }

    private static InvalidOperationException CreateTypeMismatch(AssetTypeValueField field, JsonElement value)
    {
        return new InvalidOperationException(
            $"Cannot assign {value.ValueKind} value '{AssetFieldMatcher.FormatJsonValue(value)}' to field '{field.FieldName}' of type {field.Value?.ValueType}.");
    }
}

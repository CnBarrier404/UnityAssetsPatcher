using System.Collections.ObjectModel;
using System.Text.Json;

namespace UnityAssetsPatcher.Application.Manifests;

public sealed record ModManifest
{
    public string Schema { get; }
    public string Name { get; }
    public string Author { get; }
    public string Version { get; }
    public string? Description { get; }
    public string? Game { get; }
    public IReadOnlyList<ModFile> Files { get; }
    public IReadOnlyList<ModPatch> Patches { get; }
    public IReadOnlyList<ModOptionalGroup> OptionalGroups { get; }

    internal ModManifest(
        string schema,
        string name,
        string author,
        string version,
        string? description,
        string? game,
        IEnumerable<ModFile?> files,
        IEnumerable<ModPatch?> patches,
        IEnumerable<ModOptionalGroup?> optionalGroups)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schema);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(author);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);

        Schema = schema;
        Name = name;
        Author = author;
        Version = version;
        Description = description;
        Game = game;
        Files = ManifestCollections.Copy(files, nameof(files));
        Patches = ManifestCollections.Copy(patches, nameof(patches));
        OptionalGroups = ManifestCollections.Copy(optionalGroups, nameof(optionalGroups));
    }
}

public sealed record ModOptionalGroup
{
    public string Name { get; }
    public string? Description { get; }
    public IReadOnlyList<ModFile> Files { get; }
    public IReadOnlyList<ModPatch> Patches { get; }

    internal ModOptionalGroup(
        string name,
        string? description,
        IEnumerable<ModFile?> files,
        IEnumerable<ModPatch?> patches)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name;
        Description = description;
        Files = ManifestCollections.Copy(files, nameof(files));
        Patches = ManifestCollections.Copy(patches, nameof(patches));
    }
}

public sealed record ModFile
{
    public string Source { get; }

    internal ModFile(string source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        Source = source;
    }
}

public sealed record ModPatch
{
    public string AssetsFileName { get; }
    public string AssetTypeName { get; }
    public IReadOnlyDictionary<string, JsonElement> Match { get; }
    public IReadOnlyList<ModSetOperation> SetOperations { get; }
    public IReadOnlyList<ModAddOperation> AddOperations { get; }
    public ModReplaceAsset? ReplaceAsset { get; }
    public string? ComponentTypeName { get; }
    public ModCopyAsset? CopyAsset { get; }

    internal ModPatch(
        string assetsFileName,
        string assetTypeName,
        IReadOnlyDictionary<string, JsonElement> match,
        IEnumerable<ModSetOperation?> setOperations,
        IEnumerable<ModAddOperation?> addOperations,
        ModReplaceAsset? replaceAsset,
        string? componentTypeName,
        ModCopyAsset? copyAsset)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetsFileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(assetTypeName);
        ArgumentNullException.ThrowIfNull(match);

        AssetsFileName = assetsFileName;
        AssetTypeName = assetTypeName;
        Match = ManifestCollections.CopyValues(match, nameof(match));
        SetOperations = ManifestCollections.Copy(setOperations, nameof(setOperations));
        AddOperations = ManifestCollections.Copy(addOperations, nameof(addOperations));
        ReplaceAsset = replaceAsset;
        ComponentTypeName = componentTypeName;
        CopyAsset = copyAsset;
    }
}

public sealed record ModSetOperation
{
    public string FieldPath { get; }
    public JsonElement From { get; }
    public JsonElement To { get; }

    internal ModSetOperation(string fieldPath, JsonElement from, JsonElement to)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldPath);

        FieldPath = fieldPath;
        From = from.Clone();
        To = to.Clone();
    }
}

public sealed record ModAddOperation
{
    public string FieldPath { get; }
    public JsonElement Value { get; }

    internal ModAddOperation(string fieldPath, JsonElement value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldPath);

        FieldPath = fieldPath;
        Value = value.Clone();
    }
}

public sealed record ModReplaceAsset
{
    public string SourceAssetsFile { get; }
    public string MatchFieldPath { get; }

    internal ModReplaceAsset(string sourceAssetsFile, string matchFieldPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceAssetsFile);
        ArgumentException.ThrowIfNullOrWhiteSpace(matchFieldPath);

        SourceAssetsFile = sourceAssetsFile;
        MatchFieldPath = matchFieldPath;
    }
}

public sealed record ModCopyAsset
{
    public string AssetTypeName { get; }
    public IReadOnlyDictionary<string, JsonElement> Match { get; }

    internal ModCopyAsset(string assetTypeName, IReadOnlyDictionary<string, JsonElement> match)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetTypeName);
        ArgumentNullException.ThrowIfNull(match);

        AssetTypeName = assetTypeName;
        Match = ManifestCollections.CopyValues(match, nameof(match));
    }
}

internal static class ManifestCollections
{
    public static IReadOnlyList<T> Copy<T>(IEnumerable<T?> values, string parameterName) where T : class
    {
        ArgumentNullException.ThrowIfNull(values, parameterName);

        T?[] nullableValues = [.. values];

        return nullableValues.Any(value => value is null)
            ? throw new ArgumentException("Manifest collections cannot contain null entries.", parameterName)
            : Array.AsReadOnly([.. nullableValues.Select(value => value!)]);
    }

    public static IReadOnlyDictionary<string, JsonElement> CopyValues(
        IReadOnlyDictionary<string, JsonElement> values,
        string parameterName)
    {
        var copy = new Dictionary<string, JsonElement>(values.Count, StringComparer.Ordinal);

        foreach ((string key, JsonElement value) in values)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key, parameterName);
            copy.Add(key, value.Clone());
        }

        return new ReadOnlyDictionary<string, JsonElement>(copy);
    }
}

using System.Collections.ObjectModel;
using UnityAssetsPatcher.Application.Repository;
using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Application.Patching;

namespace UnityAssetsPatcher.Application.Composition;

public sealed record CompositionFileTarget
{
    public RepositoryFileKind Kind { get; }
    public string RelativePath { get; }

    public CompositionFileTarget(RepositoryFileKind kind, string relativePath)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported composition file kind.");
        }

        Kind = kind;
        RelativePath = CompositionRepositoryModelValidation.NormalizeRelativePath(relativePath, nameof(relativePath));
    }
}

public sealed record CompositionRequest
{
    public string GameDirectory { get; }
    public string WorkingDirectory { get; }
    public IReadOnlyList<LayerRecord> ActiveLayers { get; }
    public string? ExcludedLayerId { get; }
    public IReadOnlyList<CompositionFileTarget> Files { get; }
    public IReadOnlyDictionary<string, string> LayerPackagePaths { get; }

    public CompositionRequest(
        string gameDirectory,
        string workingDirectory,
        IEnumerable<LayerRecord?> activeLayers,
        string? excludedLayerId,
        IEnumerable<CompositionFileTarget?> files,
        IReadOnlyDictionary<string, string>? layerPackagePaths = null)
    {
        GameDirectory = TrustedPath.NormalizeAbsolutePath(gameDirectory);
        WorkingDirectory = TrustedPath.NormalizeAbsolutePath(workingDirectory);
        ActiveLayers = RepositoryCollections.Copy(activeLayers, nameof(activeLayers));
        ExcludedLayerId = excludedLayerId is null
            ? null
            : CompositionRepositoryModelValidation.NormalizeIdentifier(excludedLayerId, nameof(excludedLayerId));
        Files = RepositoryCollections.Copy(files, nameof(files));
        LayerPackagePaths = CopyLayerPackagePaths(layerPackagePaths);

        EnsureUniqueFiles(Files);
    }

    private static IReadOnlyDictionary<string, string> CopyLayerPackagePaths(
        IReadOnlyDictionary<string, string>? layerPackagePaths)
    {
        Dictionary<string, string> paths = new(TrustedPath.PathComparer);

        if (layerPackagePaths is null)
        {
            return new ReadOnlyDictionary<string, string>(paths);
        }

        foreach ((string layerId, string packagePath) in layerPackagePaths)
        {
            string normalizedLayerId = CompositionRepositoryModelValidation.NormalizeIdentifier(
                layerId,
                nameof(layerPackagePaths));
            string normalizedPackagePath = TrustedPath.NormalizeAbsolutePath(packagePath);

            if (!paths.TryAdd(normalizedLayerId, normalizedPackagePath))
            {
                throw new ArgumentException(
                    $"The composition request contains duplicate package paths for layer: '{normalizedLayerId}'.",
                    nameof(layerPackagePaths));
            }
        }

        return new ReadOnlyDictionary<string, string>(paths);
    }

    private static void EnsureUniqueFiles(IReadOnlyList<CompositionFileTarget> files)
    {
        var seen = new HashSet<string>(TrustedPath.PathComparer);

        foreach (CompositionFileTarget file in files)
        {
            string key = $"{file.Kind}:{file.RelativePath}";

            if (!seen.Add(key))
            {
                throw new ArgumentException(
                    $"The composition request contains duplicate file entries: '{file.RelativePath}'.",
                    nameof(files));
            }
        }
    }
}

public sealed record CompositionFileResult
{
    public RepositoryFileKind Kind { get; }
    public string RelativePath { get; }
    public string? PreparedPath { get; }
    public bool DeletesFile => PreparedPath is null;

    public CompositionFileResult(RepositoryFileKind kind, string relativePath, string? preparedPath)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported composition file kind.");
        }

        Kind = kind;
        RelativePath = CompositionRepositoryModelValidation.NormalizeRelativePath(relativePath, nameof(relativePath));
        PreparedPath = preparedPath is null ? null : TrustedPath.NormalizeAbsolutePath(preparedPath);
    }
}

public sealed record CompositionResult
{
    public IReadOnlyList<CompositionFileResult> Files { get; }

    public CompositionResult(IEnumerable<CompositionFileResult?> files)
    {
        Files = RepositoryCollections.Copy(files, nameof(files));
    }
}

public sealed record CompositionFailure
{
    public string LayerId { get; }
    public string RelativePath { get; }
    public IReadOnlyList<PatchDiagnostic> Diagnostics { get; }

    public CompositionFailure(
        string layerId,
        string relativePath,
        IEnumerable<PatchDiagnostic?> diagnostics)
    {
        LayerId = CompositionRepositoryModelValidation.NormalizeIdentifier(layerId, nameof(layerId));
        RelativePath = CompositionRepositoryModelValidation.NormalizeRelativePath(relativePath, nameof(relativePath));
        Diagnostics = RepositoryCollections.Copy(diagnostics, nameof(diagnostics));

        if (Diagnostics.Count == 0)
        {
            throw new ArgumentException("Composition failure must contain at least one diagnostic.",
                nameof(diagnostics));
        }
    }
}

public abstract record CompositionOutcome;

public sealed record CompositionSucceeded(CompositionResult Result) : CompositionOutcome;

public sealed record CompositionFailed(CompositionFailure Failure) : CompositionOutcome;

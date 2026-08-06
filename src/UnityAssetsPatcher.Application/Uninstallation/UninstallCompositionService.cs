using UnityAssetsPatcher.Application.Repository;
using UnityAssetsPatcher.Application.Composition;
using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Application.Patching;

namespace UnityAssetsPatcher.Application.Uninstallation;

public sealed record UninstallCompositionAnalysis(
    LayerRecord TargetLayer,
    string GameDirectory,
    IReadOnlyList<LayerRecord> ActiveLayers,
    IReadOnlyList<CompositionFileTarget> Files,
    CompositionResult Current,
    CompositionResult WithoutTarget);

public sealed record UninstallCompositionFailure(
    LayerRecord Layer,
    string RelativePath,
    PatchDiagnostic Diagnostic);

public sealed class UninstallCompositionException : InvalidOperationException
{
    public IReadOnlyList<UninstallCompositionFailure> Failures { get; }

    public UninstallCompositionException(IEnumerable<UninstallCompositionFailure?> failures)
        : base("Uninstall composition failed because a remaining layer could not be replayed.")
    {
        ArgumentNullException.ThrowIfNull(failures);

        UninstallCompositionFailure?[] nullableFailures = [.. failures];

        if (nullableFailures.Any(failure => failure is null))
        {
            throw new ArgumentException("Composition failures cannot contain null entries.", nameof(failures));
        }

        Failures = Array.AsReadOnly([.. nullableFailures.Select(failure => failure!)]);

        if (Failures.Count == 0)
        {
            throw new ArgumentException("At least one composition failure is required.", nameof(failures));
        }
    }
}

public sealed class UninstallCompositionService
{
    private readonly ICompositionRepository _compositionRepository;
    private readonly ModComposer _modComposer;
    private readonly TrustedPathResolver _pathResolver;

    public UninstallCompositionService(
        ICompositionRepository compositionRepository,
        ModComposer modComposer,
        IFileSystemOperations fileSystemOperations)
    {
        ArgumentNullException.ThrowIfNull(compositionRepository);
        ArgumentNullException.ThrowIfNull(modComposer);
        ArgumentNullException.ThrowIfNull(fileSystemOperations);

        _compositionRepository = compositionRepository;
        _modComposer = modComposer;
        _pathResolver = new TrustedPathResolver(fileSystemOperations);
    }

    public UninstallCompositionAnalysis Analyze(
        LayerRecord targetLayer,
        string gameDirectory,
        string workingDirectory)
    {
        ArgumentNullException.ThrowIfNull(targetLayer);
        ArgumentException.ThrowIfNullOrWhiteSpace(gameDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);

        string normalizedGameDirectory = _pathResolver.ResolveExistingDirectory(gameDirectory);
        string fingerprint = GameInstanceIdentity.CreateFingerprint(_pathResolver, normalizedGameDirectory);

        if (!TrustedPath.PathComparer.Equals(targetLayer.GameInstanceFingerprint, fingerprint))
        {
            throw new InvalidOperationException("The selected game directory does not match the layer game instance.");
        }

        string normalizedWorkingDirectory = _pathResolver.ResolveExistingDirectory(workingDirectory);
        LayerRecord[] activeLayers = GetActiveLayers(fingerprint, targetLayer.Id);
        CompositionFileTarget[] files = CreateFileTargets(targetLayer);
        CompositionResult current = Compose(
            normalizedGameDirectory,
            normalizedWorkingDirectory,
            activeLayers,
            null,
            files);
        CompositionResult withoutTarget = Compose(
            normalizedGameDirectory,
            normalizedWorkingDirectory,
            activeLayers,
            targetLayer.Id,
            files);

        return new UninstallCompositionAnalysis(
            targetLayer,
            normalizedGameDirectory,
            activeLayers,
            files,
            current,
            withoutTarget);
    }

    private LayerRecord[] GetActiveLayers(string fingerprint, string targetLayerId)
    {
        LayerRecord[] layers =
        [
            .. _compositionRepository.Layers
                .ListLayers()
                .Select(entry => entry.Record)
                .Where(layer => TrustedPath.PathComparer.Equals(layer.GameInstanceFingerprint, fingerprint))
                .OrderBy(layer => layer.InstallSequence)
                .ThenBy(layer => layer.Id, StringComparer.Ordinal),
        ];

        if (!layers.Any(layer => TrustedPath.PathComparer.Equals(layer.Id, targetLayerId)))
        {
            throw new KeyNotFoundException($"Layer not found: {targetLayerId}");
        }

        return layers;
    }

    private static CompositionFileTarget[] CreateFileTargets(LayerRecord targetLayer)
    {
        CompositionFileTarget[] targets =
        [
            .. targetLayer.AssetsTargets.Select(path => new CompositionFileTarget(RepositoryFileKind.Assets, path)),
            .. targetLayer.PayloadTargets.Select(path => new CompositionFileTarget(RepositoryFileKind.Payload, path)),
        ];

        var seenPaths = new HashSet<string>(TrustedPath.PathComparer);

        foreach (CompositionFileTarget target in targets)
        {
            if (!seenPaths.Add(target.RelativePath))
            {
                throw new InvalidDataException(
                    $"Layer '{targetLayer.Id}' contains duplicate uninstall target path: {target.RelativePath}");
            }
        }

        return targets;
    }

    private CompositionResult Compose(
        string gameDirectory,
        string workingDirectory,
        IReadOnlyList<LayerRecord> activeLayers,
        string? excludedLayerId,
        IReadOnlyList<CompositionFileTarget> files)
    {
        CompositionOutcome outcome = _modComposer.Compose(new CompositionRequest(
            gameDirectory,
            workingDirectory,
            activeLayers,
            excludedLayerId,
            files));

        return outcome switch
        {
            CompositionSucceeded succeeded => succeeded.Result,
            CompositionFailed failed => throw CreateFailure(activeLayers, failed.Failure),
            _ => throw new ArgumentOutOfRangeException(nameof(outcome)),
        };
    }

    private static UninstallCompositionException CreateFailure(
        IReadOnlyList<LayerRecord> activeLayers,
        CompositionFailure failure)
    {
        LayerRecord layer = activeLayers.FirstOrDefault(candidate =>
                                TrustedPath.PathComparer.Equals(candidate.Id, failure.LayerId)) ??
                            throw new InvalidDataException(
                                $"Composition failure references an unknown layer: {failure.LayerId}");

        return new UninstallCompositionException(
        [
            new UninstallCompositionFailure(layer, failure.RelativePath, failure.Diagnostics[0]),
        ]);
    }
}

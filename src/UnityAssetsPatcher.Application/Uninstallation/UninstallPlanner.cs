using UnityAssetsPatcher.Application.Repository;
using UnityAssetsPatcher.Application.Composition;
using UnityAssetsPatcher.Application.Features.Uninstall;
using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Application.Installation;
using UnityAssetsPatcher.Domain.Integrity;

namespace UnityAssetsPatcher.Application.Uninstallation;

public sealed record UninstallPlan(
    string LayerDirectory,
    string GameDirectory,
    LayerRecord Layer);

public sealed class UninstallPlanner
{
    private readonly RepositoryService _repositoryService;
    private readonly IRepositoryStore _repositoryStore;
    private readonly GameDirectoryResolver _gameDirectoryResolver;
    private readonly UninstallCompositionService _compositionService;
    private readonly IFileSystemOperations _fileSystemOperations;
    private readonly TrustedPathResolver _pathResolver;

    public UninstallPlanner(
        RepositoryService repositoryService,
        IRepositoryStore repositoryStore,
        GameDirectoryResolver gameDirectoryResolver,
        UninstallCompositionService compositionService,
        IFileSystemOperations fileSystemOperations)
    {
        ArgumentNullException.ThrowIfNull(repositoryService);
        ArgumentNullException.ThrowIfNull(repositoryStore);
        ArgumentNullException.ThrowIfNull(gameDirectoryResolver);
        ArgumentNullException.ThrowIfNull(compositionService);
        ArgumentNullException.ThrowIfNull(fileSystemOperations);

        _repositoryService = repositoryService;
        _repositoryStore = repositoryStore;
        _gameDirectoryResolver = gameDirectoryResolver;
        _compositionService = compositionService;
        _fileSystemOperations = fileSystemOperations;
        _pathResolver = new TrustedPathResolver(fileSystemOperations);
    }

    public async Task<UninstallPreviewResult> BuildPreviewAsync(
        UninstallPreviewRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        _ = _repositoryService.LoadMetadata();

        LayerRecordEntry entry = ResolveLayer(request.InstallId);
        LayerRecord layer = entry.Record;
        string gameDirectory = ResolveGameDirectory(request.GameDirectory, layer);
        ValidateLayer(layer, gameDirectory);
        string workingDirectory = CreateWorkingDirectory();
        Exception? operationFailure = null;

        try
        {
            UninstallCompositionAnalysis analysis;

            try
            {
                analysis = await _compositionService.AnalyzeAsync(
                    layer,
                    gameDirectory,
                    workingDirectory,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (UninstallCompositionException exception)
            {
                return new UninstallPreviewResult(
                    layer.Id,
                    layer.ModName,
                    layer.ModVersion,
                    layer.InstalledAt,
                    gameDirectory,
                    false,
                    ToDependencyFailures(exception.Failures),
                    []);
            }

            var changedFiles = CreateChangedFiles(analysis);
            bool canUninstall = changedFiles.All(file => IsCurrentStateSafe(analysis, file));

            return new UninstallPreviewResult(
                layer.Id,
                layer.ModName,
                layer.ModVersion,
                layer.InstalledAt,
                gameDirectory,
                canUninstall,
                [],
                changedFiles);
        }
        catch (Exception failure)
        {
            operationFailure = failure;
            throw;
        }
        finally
        {
            try
            {
                DeleteWorkingDirectory(workingDirectory);
            }
            catch (Exception cleanupFailure) when (operationFailure is not null)
            {
                throw new AggregateException(operationFailure, cleanupFailure);
            }
        }
    }

    public async Task<UninstallPlan> BuildUninstallAsync(
        UninstallModRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        _ = _repositoryService.LoadMetadata();

        LayerRecordEntry entry = ResolveLayer(request.InstallId);
        LayerRecord layer = entry.Record;
        string gameDirectory = ResolveGameDirectory(request.GameDirectory, layer);
        ValidateLayer(layer, gameDirectory);
        string workingDirectory = CreateWorkingDirectory();
        Exception? operationFailure = null;

        try
        {
            UninstallCompositionAnalysis analysis;

            try
            {
                analysis = await _compositionService.AnalyzeAsync(
                    layer,
                    gameDirectory,
                    workingDirectory,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (UninstallCompositionException exception)
            {
                throw CreateDependencyException(layer, exception.Failures);
            }

            var changedFiles = CreateChangedFiles(analysis);

            if (changedFiles.Any(file => !IsCurrentStateSafe(analysis, file)))
            {
                string[] modifiedFiles = changedFiles
                    .Where(file => !IsCurrentStateSafe(analysis, file))
                    .Select(file => file.RelativePath)
                    .ToArray();
                throw new UninstallValidationException(
                    "Cannot uninstall because the current game files differ from the composed active layers: " +
                    string.Join(", ", modifiedFiles));
            }

            return new UninstallPlan(entry.LayerDirectory, gameDirectory, layer);
        }
        catch (Exception failure)
        {
            operationFailure = failure;
            throw;
        }
        finally
        {
            try
            {
                DeleteWorkingDirectory(workingDirectory);
            }
            catch (Exception cleanupFailure) when (operationFailure is not null)
            {
                throw new AggregateException(operationFailure, cleanupFailure);
            }
        }
    }

    internal UninstallChangedFileResult[] CreateChangedFiles(UninstallCompositionAnalysis analysis)
    {
        ArgumentNullException.ThrowIfNull(analysis);

        return
        [
            .. analysis.Files.Select(file => CreateChangedFile(analysis, file))
        ];
    }

    internal static bool IsCurrentStateSafe(
        UninstallCompositionAnalysis analysis,
        UninstallChangedFileResult changedFile)
    {
        CompositionFileTarget target = analysis.Files.First(file =>
            TrustedPath.PathComparer.Equals(file.RelativePath, changedFile.RelativePath));
        CompositionFileResult current = FindCompositionFile(analysis.Current, target);

        return current.PreparedPath is null
            ? changedFile.Status == FileIntegrityStatus.Missing
            : changedFile.Status == FileIntegrityStatus.Matches;
    }

    private UninstallChangedFileResult CreateChangedFile(
        UninstallCompositionAnalysis analysis,
        CompositionFileTarget target)
    {
        CompositionFileResult current = FindCompositionFile(analysis.Current, target);
        CompositionFileResult withoutTarget = FindCompositionFile(analysis.WithoutTarget, target);
        string gamePath = _pathResolver.ResolveWithinDirectory(analysis.GameDirectory, target.RelativePath);
        FileIntegrityStatus status = InspectCurrentFile(gamePath, current.PreparedPath);
        UninstallChangedFileAction action = DetermineAction(analysis, target, withoutTarget);

        return new UninstallChangedFileResult(target.RelativePath, action, status);
    }

    private UninstallChangedFileAction DetermineAction(
        UninstallCompositionAnalysis analysis,
        CompositionFileTarget target,
        CompositionFileResult withoutTarget)
    {
        if (withoutTarget.PreparedPath is null)
        {
            return UninstallChangedFileAction.Delete;
        }

        bool remainingLayerTouchesFile = analysis.ActiveLayers
            .Where(layer => layer.Enabled && !TrustedPath.PathComparer.Equals(layer.Id, analysis.TargetLayer.Id))
            .Any(layer => target.Kind == RepositoryFileKind.Assets
                ? layer.AssetsTargets.Contains(target.RelativePath, TrustedPath.PathComparer)
                : layer.PayloadTargets.Contains(target.RelativePath, TrustedPath.PathComparer));

        return remainingLayerTouchesFile
            ? UninstallChangedFileAction.Rebuild
            : UninstallChangedFileAction.RestoreBase;
    }

    private FileIntegrityStatus InspectCurrentFile(string path, string? expectedPreparedPath)
    {
        if (!TryGetAttributes(path, out FileAttributes attributes))
        {
            return FileIntegrityStatus.Missing;
        }

        if (attributes.HasFlag(FileAttributes.Directory) || attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            return FileIntegrityStatus.Unreadable;
        }

        if (expectedPreparedPath is null)
        {
            return FileIntegrityStatus.Modified;
        }

        try
        {
            FileIntegrity expected = _fileSystemOperations.ComputeFileIntegrity(expectedPreparedPath);
            return expected.Matches(_fileSystemOperations.ComputeFileIntegrity(path))
                ? FileIntegrityStatus.Matches
                : FileIntegrityStatus.Modified;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return FileIntegrityStatus.Unreadable;
        }
    }

    private LayerRecordEntry ResolveLayer(string layerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(layerId);

        var matches = _repositoryStore.Layers
            .ListLayers()
            .Where(entry => TrustedPath.PathComparer.Equals(entry.Record.Id, layerId))
            .ToArray();

        if (matches.Length == 1)
        {
            return matches[0];
        }

        if (matches.Length > 1)
        {
            throw new UninstallValidationException($"Multiple layers use ID '{layerId}'.");
        }

        throw new KeyNotFoundException($"Install record not found: {layerId}");
    }

    private void ValidateLayer(LayerRecord layer, string gameDirectory)
    {
        RepositoryMetadata metadata = _repositoryService.LoadMetadata();

        if (!string.Equals(layer.RepositoryId, metadata.RepositoryId, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Layer record does not belong to this backup repository.");
        }

        string fingerprint = GameInstanceIdentity.CreateFingerprint(_fileSystemOperations, gameDirectory);

        if (!string.Equals(layer.GameInstanceFingerprint, fingerprint, StringComparison.Ordinal))
        {
            throw new UninstallValidationException(
                "The selected game directory does not match the install record game instance.");
        }
    }

    private string ResolveGameDirectory(string? requestedGameDirectory, LayerRecord layer)
    {
        if (requestedGameDirectory is null && string.IsNullOrWhiteSpace(layer.GameName))
        {
            throw new DirectoryNotFoundException(
                "Game directory was not provided and install layer does not contain a game name.");
        }

        return _gameDirectoryResolver.ResolveRequired(requestedGameDirectory, layer.GameName);
    }

    private string CreateWorkingDirectory()
    {
        string workingDirectory = Path.Combine(
            AppConfig.TemporaryDirectory,
            $"UnityAssetsPatcher.Uninstall.{Guid.NewGuid():N}");

        _fileSystemOperations.EnsureDirectory(workingDirectory);

        return _pathResolver.ResolveExistingDirectory(workingDirectory);
    }

    private void DeleteWorkingDirectory(string workingDirectory)
    {
        if (Directory.Exists(workingDirectory))
        {
            _fileSystemOperations.DeleteDirectoryTree(workingDirectory);
        }
    }

    private static CompositionFileResult FindCompositionFile(
        CompositionResult composition,
        CompositionFileTarget target)
    {
        return composition.Files.FirstOrDefault(file =>
                   file.Kind == target.Kind &&
                   TrustedPath.PathComparer.Equals(file.RelativePath, target.RelativePath)) ??
               throw new InvalidDataException($"Composition result is missing file: {target.RelativePath}");
    }

    private static UninstallDependencyFailureResult[] ToDependencyFailures(
        IReadOnlyList<UninstallCompositionFailure> failures)
    {
        return
        [
            .. failures.Select(failure => new UninstallDependencyFailureResult(
                failure.Layer.ModName,
                failure.Layer.ModVersion,
                failure.RelativePath,
                failure.Diagnostic))
        ];
    }

    private static UninstallValidationException CreateDependencyException(
        LayerRecord targetLayer,
        IReadOnlyList<UninstallCompositionFailure> failures)
    {
        string details = string.Join(
            "; ",
            failures.Select(failure =>
                $"{failure.Layer.ModName} {failure.Layer.ModVersion} at {failure.RelativePath} " +
                $"({failure.Diagnostic.Code})"));

        return new UninstallValidationException(
            $"Cannot uninstall {targetLayer.ModName} because remaining layers have real patch dependencies: " +
            details);
    }

    private bool TryGetAttributes(string path, out FileAttributes attributes)
    {
        try
        {
            attributes = _fileSystemOperations.GetAttributes(path);

            return true;
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            attributes = default;

            return false;
        }
    }
}

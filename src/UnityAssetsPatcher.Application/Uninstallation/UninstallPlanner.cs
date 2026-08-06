using UnityAssetsPatcher.Application.Repository;
using UnityAssetsPatcher.Application.Composition;
using UnityAssetsPatcher.Application.Contracts;
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
    private readonly ICompositionRepository _compositionRepository;
    private readonly GameDirectoryResolver _gameDirectoryResolver;
    private readonly UninstallCompositionService _compositionService;
    private readonly IFileSystemOperations _fileSystemOperations;
    private readonly TrustedPathResolver _pathResolver;

    public UninstallPlanner(
        RepositoryService repositoryService,
        ICompositionRepository compositionRepository,
        GameDirectoryResolver gameDirectoryResolver,
        UninstallCompositionService compositionService,
        IFileSystemOperations fileSystemOperations)
    {
        ArgumentNullException.ThrowIfNull(repositoryService);
        ArgumentNullException.ThrowIfNull(compositionRepository);
        ArgumentNullException.ThrowIfNull(gameDirectoryResolver);
        ArgumentNullException.ThrowIfNull(compositionService);
        ArgumentNullException.ThrowIfNull(fileSystemOperations);

        _repositoryService = repositoryService;
        _compositionRepository = compositionRepository;
        _gameDirectoryResolver = gameDirectoryResolver;
        _compositionService = compositionService;
        _fileSystemOperations = fileSystemOperations;
        _pathResolver = new TrustedPathResolver(fileSystemOperations);
    }

    public IReadOnlyList<InstallRecordSummary> ListInstalled()
    {
        RepositoryMetadata metadata = _repositoryService.LoadMetadata();

        if (metadata.FormatVersion == RepositoryService.LegacyRepositoryFormatVersion)
        {
            return _repositoryService.ListLegacyInstalled();
        }

        return _compositionRepository.Layers
            .ListLayers()
            .Select(entry => new InstallRecordSummary(
                entry.Record.Id,
                entry.Record.ModName,
                entry.Record.ModVersion,
                entry.Record.GameName,
                entry.Record.InstalledAt))
            .ToArray();
    }

    public UninstallPreviewResult BuildPreview(UninstallPreviewRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        LayerRecordEntry entry = ResolveLayer(request.InstallId);
        LayerRecord layer = entry.Record;
        string gameDirectory = ResolveGameDirectory(request.GameDirectory, layer);
        ValidateLayer(layer, gameDirectory);
        string workingDirectory = CreateWorkingDirectory();

        try
        {
            UninstallCompositionAnalysis analysis;

            try
            {
                analysis = _compositionService.Analyze(layer, gameDirectory, workingDirectory);
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

            UninstallChangedFileResult[] changedFiles = CreateChangedFiles(analysis);
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
        finally
        {
            DeleteWorkingDirectory(workingDirectory);
        }
    }

    public UninstallPlan BuildUninstall(UninstallModRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        LayerRecordEntry entry = ResolveLayer(request.InstallId);
        LayerRecord layer = entry.Record;
        string gameDirectory = ResolveGameDirectory(request.GameDirectory, layer);
        ValidateLayer(layer, gameDirectory);
        string workingDirectory = CreateWorkingDirectory();

        try
        {
            UninstallCompositionAnalysis analysis;

            try
            {
                analysis = _compositionService.Analyze(layer, gameDirectory, workingDirectory);
            }
            catch (UninstallCompositionException exception)
            {
                throw CreateDependencyException(layer, exception.Failures);
            }

            UninstallChangedFileResult[] changedFiles = CreateChangedFiles(analysis);

            if (changedFiles.Any(file => !IsCurrentStateSafe(analysis, file)))
            {
                string[] modifiedFiles = changedFiles
                    .Where(file => !IsCurrentStateSafe(analysis, file))
                    .Select(file => file.RelativePath)
                    .ToArray();
                throw new InvalidOperationException(
                    "Cannot uninstall because the current game files differ from the composed active layers: " +
                    string.Join(", ", modifiedFiles));
            }

            return new UninstallPlan(entry.LayerDirectory, gameDirectory, layer);
        }
        finally
        {
            DeleteWorkingDirectory(workingDirectory);
        }
    }

    internal UninstallChangedFileResult[] CreateChangedFiles(UninstallCompositionAnalysis analysis)
    {
        ArgumentNullException.ThrowIfNull(analysis);

        return
        [
            .. analysis.Files.Select(file => CreateChangedFile(analysis, file)),
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
            return _fileSystemOperations.MatchesFile(path, expected)
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

        LayerRecordEntry[] matches = _compositionRepository.Layers
            .ListLayers()
            .Where(entry => TrustedPath.PathComparer.Equals(entry.Record.Id, layerId))
            .ToArray();

        if (matches.Length == 1)
        {
            return matches[0];
        }

        if (matches.Length > 1)
        {
            throw new InvalidOperationException($"Multiple layers use ID '{layerId}'.");
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
            throw new InvalidOperationException(
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
            Path.GetTempPath(),
            $"UnityAssetsPatcher.Uninstall.{Guid.NewGuid():N}");
        _fileSystemOperations.EnsureDirectory(workingDirectory);

        return _pathResolver.ResolveExistingDirectory(workingDirectory);
    }

    private void DeleteWorkingDirectory(string workingDirectory)
    {
        if (Directory.Exists(workingDirectory))
        {
            _fileSystemOperations.DeleteDirectory(workingDirectory);
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
                failure.Diagnostic)),
        ];
    }

    private static InvalidOperationException CreateDependencyException(
        LayerRecord targetLayer,
        IReadOnlyList<UninstallCompositionFailure> failures)
    {
        string details = string.Join(
            "; ",
            failures.Select(failure =>
                $"{failure.Layer.ModName} {failure.Layer.ModVersion} at {failure.RelativePath} " +
                $"({failure.Diagnostic.Code})"));

        return new InvalidOperationException(
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

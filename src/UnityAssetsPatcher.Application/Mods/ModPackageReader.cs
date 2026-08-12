using Microsoft.Extensions.Logging;
using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Application.Operations;

namespace UnityAssetsPatcher.Application.Mods;

public sealed class ModPackageReader
{
    private readonly IModArchiveReader _archiveReader;
    private readonly IFileSystemOperations _fileSystemOperations;
    private readonly ILoggerFactory _loggerFactory;

    public ModPackageReader(
        IModArchiveReader archiveReader,
        IFileSystemOperations fileSystemOperations,
        ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(archiveReader);
        ArgumentNullException.ThrowIfNull(fileSystemOperations);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        _archiveReader = archiveReader;
        _fileSystemOperations = fileSystemOperations;
        _loggerFactory = loggerFactory;
    }

    public async Task<OperationResult<ModPackage>> OpenAsync(
        string packagePath,
        IReadOnlyList<string> selectedOptionalGroups,
        StepTimer timings,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);
        ArgumentNullException.ThrowIfNull(selectedOptionalGroups);
        ArgumentNullException.ThrowIfNull(timings);
        cancellationToken.ThrowIfCancellationRequested();

        string fullPackagePath = Path.GetFullPath(packagePath);
        var sessionResult = await OpenSessionAsync(
                fullPackagePath,
                cancellationToken)
            .ConfigureAwait(false);

        if (sessionResult is OperationFailed<ModPackageSession> sessionFailure)
        {
            return new OperationFailed<ModPackage>(sessionFailure.Error);
        }

        ModPackageSession session = ((OperationSucceeded<ModPackageSession>)sessionResult).Value;
        string? temporaryDirectory = null;
        bool sessionTransferred = false;

        try
        {
            var contentResult = await ReadContentMeasuredAsync(
                    timings,
                    session,
                    fullPackagePath,
                    selectedOptionalGroups,
                    cancellationToken)
                .ConfigureAwait(false);

            if (contentResult is OperationFailed<ModPackageContent> contentFailure)
            {
                return new OperationFailed<ModPackage>(contentFailure.Error);
            }

            ModPackageContent content = ((OperationSucceeded<ModPackageContent>)contentResult).Value;
            var sourcesResult = await ExtractPatchSourcesMeasuredAsync(
                    timings,
                    session,
                    content.Selection.EffectiveManifest,
                    cancellationToken)
                .ConfigureAwait(false);

            if (sourcesResult is OperationFailed<PreparedSources> sourcesFailure)
            {
                return new OperationFailed<ModPackage>(sourcesFailure.Error);
            }

            PreparedSources sources = ((OperationSucceeded<PreparedSources>)sourcesResult).Value;
            temporaryDirectory = sources.TemporaryDirectory;
            var package = new ModPackage(
                content.SourceManifest,
                content.Selection.EffectiveManifest,
                content.Selection.AppliedOptionalGroups,
                sources.Paths,
                session,
                _fileSystemOperations,
                temporaryDirectory);
            sessionTransferred = true;
            temporaryDirectory = null;

            return new OperationSucceeded<ModPackage>(package);
        }
        finally
        {
            if (!sessionTransferred)
            {
                session.Dispose();
            }

            DeleteTemporaryDirectory(temporaryDirectory);
        }
    }

    public async Task<OperationResult<byte[]>> ReadManifestAsync(
        string packagePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);
        cancellationToken.ThrowIfCancellationRequested();

        string fullPackagePath = Path.GetFullPath(packagePath);

        try
        {
            using IModArchiveSession package = await _archiveReader
                .OpenAsync(fullPackagePath, cancellationToken)
                .ConfigureAwait(false);
            var entryResult = ModPackageManifest.FindEntry(
                package,
                fullPackagePath,
                cancellationToken);

            if (entryResult is OperationFailed<IModArchiveEntry> entryFailure)
            {
                return new OperationFailed<byte[]>(entryFailure.Error);
            }

            IModArchiveEntry manifestEntry = ((OperationSucceeded<IModArchiveEntry>)entryResult).Value;

            return await ModPackageManifest.ReadAsync(
                manifestEntry,
                fullPackagePath,
                _loggerFactory.CreateLogger<ModPackageReader>(),
                cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidDataException)
        {
            return InvalidArchive<byte[]>(fullPackagePath);
        }
    }

    private async Task<OperationResult<ModPackageSession>> OpenSessionAsync(
        string packagePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);
        cancellationToken.ThrowIfCancellationRequested();

        string fullPackagePath = Path.GetFullPath(packagePath);
        IModArchiveSession? archive = null;

        try
        {
            archive = await _archiveReader
                .OpenAsync(fullPackagePath, cancellationToken)
                .ConfigureAwait(false);
            var indexResult = ModPackageValidator.Validate(
                archive,
                fullPackagePath,
                cancellationToken);

            if (indexResult is OperationFailed<ModPackageIndex> indexFailure)
            {
                return new OperationFailed<ModPackageSession>(indexFailure.Error);
            }

            ModPackageIndex index = ((OperationSucceeded<ModPackageIndex>)indexResult).Value;
            var session = new ModPackageSession(
                fullPackagePath,
                archive,
                index,
                _fileSystemOperations,
                _loggerFactory.CreateLogger<ModPackageSession>());
            archive = null;

            return new OperationSucceeded<ModPackageSession>(session);
        }
        catch (InvalidDataException)
        {
            return InvalidArchive<ModPackageSession>(fullPackagePath);
        }
        finally
        {
            archive?.Dispose();
        }
    }

    private static Task<OperationResult<ModPackageContent>> ReadContentMeasuredAsync(
        StepTimer timings,
        ModPackageSession session,
        string packagePath,
        IReadOnlyList<string> selectedOptionalGroups,
        CancellationToken cancellationToken)
    {
        return timings.MeasureAsync(
            "read-package",
            () => ReadContentAsync(session, packagePath, selectedOptionalGroups, cancellationToken));
    }

    private Task<OperationResult<PreparedSources>> ExtractPatchSourcesMeasuredAsync(
        StepTimer timings,
        ModPackageSession session,
        ModManifest manifest,
        CancellationToken cancellationToken)
    {
        return timings.MeasureAsync(
            "prepare-sources",
            () => ExtractPatchSourcesAsync(session, manifest, cancellationToken));
    }

    private static async Task<OperationResult<ModPackageContent>> ReadContentAsync(
        ModPackageSession session,
        string packagePath,
        IReadOnlyList<string> selectedOptionalGroups,
        CancellationToken cancellationToken)
    {
        var bytesResult = await session.ReadManifestAsync(cancellationToken)
            .ConfigureAwait(false);

        if (bytesResult is OperationFailed<byte[]> bytesFailure)
        {
            return new OperationFailed<ModPackageContent>(bytesFailure.Error);
        }

        byte[] manifestBytes = ((OperationSucceeded<byte[]>)bytesResult).Value;
        var manifestResult = ModManifestParser.Parse(manifestBytes);

        if (manifestResult is OperationFailed<ModManifest> manifestFailure)
        {
            return new OperationFailed<ModPackageContent>(manifestFailure.Error);
        }

        ModManifest manifest = ((OperationSucceeded<ModManifest>)manifestResult).Value;
        var selectionResult = ModManifestOptionalSelector.Select(
            manifest,
            selectedOptionalGroups);

        if (selectionResult is OperationFailed<ModManifestSelection> selectionFailure)
        {
            return new OperationFailed<ModPackageContent>(selectionFailure.Error);
        }

        ModManifestSelection selection = ((OperationSucceeded<ModManifestSelection>)selectionResult).Value;
        ModFile? missingFile = selection.EffectiveManifest.Files.FirstOrDefault(file =>
            !session.ContainsEntry(file.Source));

        if (missingFile is not null)
        {
            return new OperationFailed<ModPackageContent>(new OperationError(
                ModPackageErrorCodes.MissingEntry,
                new Dictionary<string, object?>
                {
                    ["package_path"] = packagePath,
                    ["entry_path"] = missingFile.Source,
                }));
        }

        return new OperationSucceeded<ModPackageContent>(new ModPackageContent(manifest, selection));
    }

    private async Task<OperationResult<PreparedSources>> ExtractPatchSourcesAsync(
        ModPackageSession session,
        ModManifest manifest,
        CancellationToken cancellationToken)
    {
        string[] replacementSources =
        [
            .. manifest.Patches
                .Select(patch => patch.ReplaceAsset?.SourceAssetsFile)
                .OfType<string>()
                .Select(source => source.Replace('\\', '/'))
                .Distinct(StringComparer.OrdinalIgnoreCase)
        ];

        if (replacementSources.Length == 0)
        {
            var emptySources = new PreparedSources(
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                null);

            return new OperationSucceeded<PreparedSources>(emptySources);
        }

        string temporaryDirectory = Path.Combine(Path.GetTempPath(), $"UnityAssetsPatcher.{Guid.NewGuid():N}");
        bool completed = false;

        try
        {
            var paths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (string source in replacementSources)
            {
                string destinationPath = ResolveUnderDirectory(temporaryDirectory, source);
                var copyResult = await session
                    .CopyEntryToNewFileAsync(source, destinationPath, cancellationToken)
                    .ConfigureAwait(false);

                if (copyResult is OperationFailed<long> copyFailure)
                {
                    return new OperationFailed<PreparedSources>(copyFailure.Error);
                }

                paths[source] = destinationPath;
            }

            completed = true;

            return new OperationSucceeded<PreparedSources>(new PreparedSources(paths, temporaryDirectory));
        }
        finally
        {
            if (!completed)
            {
                DeleteTemporaryDirectory(temporaryDirectory);
            }
        }
    }

    private string ResolveUnderDirectory(string rootDirectory, string relativePath)
    {
        string fullRootDirectory = Path.GetFullPath(rootDirectory);
        string fullPath = Path.GetFullPath(Path.Combine(
            fullRootDirectory,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));

        if (!_fileSystemOperations.IsPathWithinDirectory(fullPath, fullRootDirectory))
        {
            throw new InvalidDataException(
                $"Package payload source cannot escape its extraction directory: {relativePath}");
        }

        return fullPath;
    }

    private void DeleteTemporaryDirectory(string? temporaryDirectory)
    {
        if (temporaryDirectory is not null && Directory.Exists(temporaryDirectory))
        {
            _fileSystemOperations.DeleteDirectoryTree(temporaryDirectory);
        }
    }

    private static OperationFailed<T> InvalidArchive<T>(string packagePath)
    {
        return new OperationFailed<T>(new OperationError(
            ModPackageErrorCodes.InvalidArchive,
            new Dictionary<string, object?>
            {
                ["package_path"] = packagePath,
            }));
    }

    private sealed record ModPackageContent(ModManifest SourceManifest, ModManifestSelection Selection);

    private sealed record PreparedSources(IReadOnlyDictionary<string, string> Paths, string? TemporaryDirectory);
}

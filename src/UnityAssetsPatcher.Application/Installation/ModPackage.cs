using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Application.Manifests;
using UnityAssetsPatcher.Application.Operations;
using UnityAssetsPatcher.Application.Packages;

namespace UnityAssetsPatcher.Application.Installation;

public sealed class ModPackage : IDisposable
{
    public IReadOnlyDictionary<string, string> PatchSourcePaths { get; }
    public IReadOnlyList<ModOptionalGroup> OptionalGroups { get; }
    public IReadOnlyList<string> AppliedOptionalGroups { get; }
    public ModManifest Manifest { get; }

    private readonly ModPackageArchiveSession _archiveSession;
    private readonly IFileSystemOperations _fileSystemOperations;
    private readonly string? _temporaryDirectory;

    private ModPackage(
        ModManifest manifest,
        IReadOnlyList<ModOptionalGroup> optionalGroups,
        IReadOnlyList<string> appliedOptionalGroups,
        IReadOnlyDictionary<string, string> patchSourcePaths,
        ModPackageArchiveSession archiveSession,
        IFileSystemOperations fileSystemOperations,
        string? temporaryDirectory)
    {
        Manifest = manifest;
        OptionalGroups = optionalGroups;
        AppliedOptionalGroups = appliedOptionalGroups;
        PatchSourcePaths = patchSourcePaths;
        _archiveSession = archiveSession;
        _fileSystemOperations = fileSystemOperations;
        _temporaryDirectory = temporaryDirectory;
    }

    public static ModPackage Open(
        string modPackagePath,
        IReadOnlyList<string> selectedOptionalGroups,
        ModPackageArchiveService archiveService,
        IFileSystemOperations fileSystemOperations,
        StepTimer timings)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modPackagePath);
        ArgumentNullException.ThrowIfNull(selectedOptionalGroups);
        ArgumentNullException.ThrowIfNull(archiveService);
        ArgumentNullException.ThrowIfNull(fileSystemOperations);
        ArgumentNullException.ThrowIfNull(timings);

        string modPackageFullPath = Path.GetFullPath(modPackagePath);
        ModPackageArchiveSession archiveSession = RequireResult(
            archiveService.OpenRead(modPackageFullPath));

        try
        {
            (ModManifest sourceManifest, ModManifestSelection selection) = timings.Measure(
                "read-package",
                () =>
                {
                    byte[] manifestBytes = RequireResult(archiveSession.ReadManifest());
                    ModManifest manifest = RequireResult(ModManifestParser.Parse(manifestBytes));
                    ModManifestSelection selected = RequireResult(
                        ModManifestOptionalSelector.Select(manifest, selectedOptionalGroups));

                    return (manifest, selected);
                });

            (var patchSourcePaths, string? temporaryDirectory) =
                timings.Measure(
                    "prepare-sources",
                    () => ExtractPatchSources(
                        archiveSession,
                        fileSystemOperations,
                        selection.Manifest));

            return new ModPackage(
                selection.Manifest,
                sourceManifest.OptionalGroups,
                selection.AppliedOptionalGroups,
                patchSourcePaths,
                archiveSession,
                fileSystemOperations,
                temporaryDirectory);
        }
        catch
        {
            archiveSession.Dispose();

            throw;
        }
    }

    public void CopyPayloadFile(string source, string destinationPath)
    {
        RequireResult(_archiveSession.CopyEntryToNewFile(source, destinationPath));
    }

    public void Dispose()
    {
        try
        {
            _archiveSession.Dispose();
        }
        finally
        {
            if (_temporaryDirectory is not null && Directory.Exists(_temporaryDirectory))
            {
                _fileSystemOperations.DeleteDirectory(_temporaryDirectory);
            }
        }
    }

    private static (IReadOnlyDictionary<string, string> Paths, string? TemporaryDirectory) ExtractPatchSources(
        ModPackageArchiveSession archiveSession,
        IFileSystemOperations fileSystemOperations,
        ModManifest manifest)
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
            return (new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase), null);
        }

        string temporaryDirectory = Path.Combine(Path.GetTempPath(), $"UnityAssetsPatcher.{Guid.NewGuid():N}");

        try
        {
            var paths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (string source in replacementSources)
            {
                string destinationPath = ResolveUnderDirectory(fileSystemOperations, temporaryDirectory, source);

                RequireResult(archiveSession.CopyEntryToNewFile(source, destinationPath));
                paths[source] = destinationPath;
            }

            return (paths, temporaryDirectory);
        }
        catch
        {
            if (Directory.Exists(temporaryDirectory))
            {
                fileSystemOperations.DeleteDirectory(temporaryDirectory);
            }

            throw;
        }
    }

    private static string ResolveUnderDirectory(
        IFileSystemOperations fileSystemOperations,
        string rootDirectory,
        string relativePath)
    {
        string fullRootDirectory = Path.GetFullPath(rootDirectory);
        string fullPath = Path.GetFullPath(Path.Combine(
            fullRootDirectory,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!fileSystemOperations.IsPathWithinDirectory(fullPath, fullRootDirectory))
        {
            throw new InvalidOperationException(
                $"Zip payload source cannot escape its extraction directory: {relativePath}");
        }

        return fullPath;
    }

    private static TResult RequireResult<TResult>(OperationResult<TResult> result)
    {
        return result switch
        {
            OperationSucceeded<TResult> succeeded => succeeded.Value,
            OperationFailed<TResult> failed => throw OperationFailure(failed.Error),
            _ => throw new ArgumentOutOfRangeException(nameof(result)),
        };
    }

    private static InvalidOperationException OperationFailure(OperationError error)
    {
        if (error.Code == ModPackageErrorCodes.ExtractionLimitExceeded)
        {
            string entryPath = error.Parameters.TryGetValue("entry_path", out object? entryPathValue)
                ? entryPathValue?.ToString() ?? "<unknown>"
                : "<unknown>";
            string limit = error.Parameters.TryGetValue("limit_bytes", out object? limitValue)
                ? limitValue?.ToString() ?? "<unknown>"
                : "<unknown>";

            return new InvalidOperationException(
                $"Zip package exceeds the maximum allowed total uncompressed size while extracting {entryPath}: " +
                $"more than {limit} bytes.");
        }

        string parameters = error.Parameters.Count == 0
            ? string.Empty
            : $" ({string.Join(", ", error.Parameters.Select(parameter => $"{parameter.Key}={parameter.Value}"))})";

        return new InvalidOperationException($"Operation '{error.Code.Value}' failed{parameters}.");
    }
}

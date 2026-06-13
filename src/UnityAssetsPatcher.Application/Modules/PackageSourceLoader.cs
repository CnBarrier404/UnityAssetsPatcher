using System.IO.Compression;
using System.Text.Json;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Manifests;

namespace UnityAssetsPatcher.Application.Modules;

public sealed class PackageSourceLoader
{
    private readonly IModManifestLoader _manifestLoader;
    private readonly GameDirectoryResolver _gameDirectoryResolver;
    private readonly Func<string, ZipArchive> _openPackageArchive;

    public PackageSourceLoader(
        IModManifestLoader manifestLoader,
        GameDirectoryResolver gameDirectoryResolver,
        Func<string, ZipArchive> openPackageArchive)
    {
        _manifestLoader = manifestLoader;
        _gameDirectoryResolver = gameDirectoryResolver;
        _openPackageArchive = openPackageArchive;
    }

    public PackageSource Execute(string packagePath, string? gameDirectory, WorkflowTiming timings)
    {
        string fullPackagePath = Path.GetFullPath(packagePath);
        ZipArchive? archive = null;

        if (!File.Exists(fullPackagePath))
        {
            throw new FileNotFoundException($"Mod zip file not found: {fullPackagePath}", fullPackagePath);
        }

        try
        {
            ModManifest manifest = timings.MeasureReadPackage(() =>
            {
                archive = _openPackageArchive(fullPackagePath);
                JsonElement manifestElement = ManifestJsonReader.ReadManifestElementFromZip(archive, fullPackagePath);

                return _manifestLoader.Load(manifestElement);
            });
            string resolvedGameDirectory = ResolveGameDirectory(gameDirectory, manifest);
            ZipArchive sourceArchive = archive ??
                                       throw new InvalidOperationException(
                                           "Package archive was not opened while reading the manifest.");
            PackageWorkspace workspace =
                timings.MeasurePrepareSources(() => PackageWorkspace.Create(fullPackagePath, manifest, sourceArchive));

            archive = null;

            return new PackageSource(fullPackagePath, sourceArchive, manifest, resolvedGameDirectory, workspace);
        }
        finally
        {
            archive?.Dispose();
        }
    }

    private string ResolveGameDirectory(string? gameDirectory, ModManifest manifest)
    {
        if (!string.IsNullOrWhiteSpace(gameDirectory))
        {
            string fullGameDirectory = Path.GetFullPath(gameDirectory);

            return Directory.Exists(fullGameDirectory)
                ? fullGameDirectory
                : throw new DirectoryNotFoundException($"Game directory not found: {fullGameDirectory}");
        }

        if (string.IsNullOrWhiteSpace(manifest.Game))
        {
            throw new DirectoryNotFoundException(
                "Game directory was not provided and manifest does not contain a 'game' property.");
        }

        string? resolvedDirectory = _gameDirectoryResolver.Resolve(manifest.Game);

        return resolvedDirectory ?? throw new DirectoryNotFoundException(
            $"Game directory could not be resolved for manifest game: {manifest.Game}");
    }
}

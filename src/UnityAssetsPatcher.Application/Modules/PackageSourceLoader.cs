using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Manifests;

namespace UnityAssetsPatcher.Application.Modules;

public sealed class PackageSourceLoader
{
    private readonly IModManifestLoader _manifestLoader;
    private readonly GameDirectoryResolver _gameDirectoryResolver;

    public PackageSourceLoader(IModManifestLoader manifestLoader, GameDirectoryResolver gameDirectoryResolver)
    {
        _manifestLoader = manifestLoader;
        _gameDirectoryResolver = gameDirectoryResolver;
    }

    public PackageSource Execute(string packagePath, string? gameDirectory, WorkflowTiming timings)
    {
        string fullPackagePath = Path.GetFullPath(packagePath);

        if (!File.Exists(fullPackagePath))
        {
            throw new FileNotFoundException($"Mod zip file not found: {fullPackagePath}", fullPackagePath);
        }

        ModManifest manifest = timings.MeasureReadPackage(() => _manifestLoader.Load(fullPackagePath));
        string resolvedGameDirectory = ResolveGameDirectory(gameDirectory, manifest);
        PackageWorkspace workspace =
            timings.MeasurePrepareSources(() => PackageWorkspace.Create(fullPackagePath, manifest));

        return new PackageSource(fullPackagePath, manifest, resolvedGameDirectory, workspace);
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

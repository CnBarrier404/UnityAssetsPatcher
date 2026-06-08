using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Manifests;

namespace UnityAssetsPatcher.Application.Installing;

public sealed class InstallSourcePreparer
{
    private readonly IModManifestLoader _manifestLoader;
    private readonly GameDirectoryResolver _gameDirectoryResolver;

    public InstallSourcePreparer(
        IModManifestLoader manifestLoader,
        GameDirectoryResolver gameDirectoryResolver)
    {
        _manifestLoader = manifestLoader;
        _gameDirectoryResolver = gameDirectoryResolver;
    }

    public PreparedInstallSource Prepare(
        string zipFilePath,
        string? gameDirectory,
        InstallTimingBuilder timings)
    {
        EnsureZipFileExists(zipFilePath);

        ModManifest manifest = timings.MeasureReadPackage(() => _manifestLoader.Load(zipFilePath));
        string resolvedGameDirectory = ResolveGameDirectory(gameDirectory, manifest);
        InstallPackageWorkspace workspace =
            timings.MeasurePrepareSources(() => InstallPackageWorkspace.Prepare(zipFilePath, manifest));

        return new PreparedInstallSource(zipFilePath, manifest, resolvedGameDirectory, workspace);
    }

    private static void EnsureZipFileExists(string zipFilePath)
    {
        if (!File.Exists(zipFilePath))
        {
            throw new FileNotFoundException($"Mod zip file not found: {zipFilePath}", zipFilePath);
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

using UnityAssetsPatcher.Application.Contracts;

namespace UnityAssetsPatcher.Application.Modules;

public sealed class PackageSource : IDisposable
{
    private readonly PackageWorkspace _workspace;

    public PackageSource(
        string packagePath,
        ModManifest manifest,
        string gameDirectory,
        PackageWorkspace workspace)
    {
        PackagePath = packagePath;
        Manifest = manifest;
        GameDirectory = gameDirectory;
        _workspace = workspace;
    }

    public string PackagePath { get; }

    public ModManifest Manifest { get; }

    public string GameDirectory { get; }

    public string ConfigPath => _workspace.ConfigPath;

    public void Dispose()
    {
        _workspace.Dispose();
    }
}

using UnityAssetsPatcher.Application.Contracts;

namespace UnityAssetsPatcher.Application.Installing;

public sealed class PreparedInstallSource : IDisposable
{
    private readonly InstallPackageWorkspace _workspace;

    public PreparedInstallSource(
        string zipFilePath,
        ModManifest manifest,
        string gameDirectory,
        InstallPackageWorkspace workspace)
    {
        ZipFilePath = zipFilePath;
        Manifest = manifest;
        GameDirectory = gameDirectory;
        _workspace = workspace;
    }

    public string ZipFilePath { get; }

    public ModManifest Manifest { get; }

    public string GameDirectory { get; }

    public string ConfigPath => _workspace.ConfigPath;

    public void Dispose()
    {
        _workspace.Dispose();
    }
}

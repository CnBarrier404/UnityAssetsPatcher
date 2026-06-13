using System.IO.Compression;
using UnityAssetsPatcher.Application.Contracts;

namespace UnityAssetsPatcher.Application.Modules;

public sealed class PackageSource : IDisposable
{
    private readonly ZipArchive _archive;
    private readonly PackageWorkspace _workspace;

    public PackageSource(
        string packagePath,
        ZipArchive archive,
        ModManifest manifest,
        string gameDirectory,
        PackageWorkspace workspace)
    {
        PackagePath = packagePath;
        _archive = archive;
        Manifest = manifest;
        GameDirectory = gameDirectory;
        _workspace = workspace;
    }

    public string PackagePath { get; }

    public ZipArchive Archive => _archive;

    public ModManifest Manifest { get; }

    public string GameDirectory { get; }

    public string ConfigPath => _workspace.ConfigPath;

    public void Dispose()
    {
        _workspace.Dispose();
        _archive.Dispose();
    }
}

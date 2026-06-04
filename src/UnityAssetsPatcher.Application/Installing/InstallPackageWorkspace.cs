using System.IO.Compression;
using UnityAssetsPatcher.Application.Contracts;

namespace UnityAssetsPatcher.Application.Installing;

public sealed class InstallPackageWorkspace : IDisposable
{
    private InstallPackageWorkspace(string configPath, string? temporaryDirectory)
    {
        ConfigPath = configPath;
        _temporaryDirectory = temporaryDirectory;
    }

    private readonly string? _temporaryDirectory;

    public string ConfigPath { get; }

    public static InstallPackageWorkspace Prepare(string zipFilePath, ModManifest manifest)
    {
        string[] relativeSources = manifest.Patches
            .Select(patch => patch.ReplaceFrom?.AssetsFilePath)
            .OfType<string>()
            .Where(source => !Path.IsPathRooted(source))
            .Select(InstallZipPaths.NormalizeZipEntryPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (relativeSources.Length == 0)
        {
            return new InstallPackageWorkspace(zipFilePath, null);
        }

        string temporaryDirectory = Path.Combine(Path.GetTempPath(), $"UnityAssetsPatcher.{Guid.NewGuid():N}");

        using ZipArchive archive = ZipFile.OpenRead(zipFilePath);

        foreach (string source in relativeSources)
        {
            ZipArchiveEntry entry = InstallZipPaths.FindRequiredZipEntry(archive, source, zipFilePath);
            string destinationPath = InstallZipPaths.ResolvePathUnderDirectory(temporaryDirectory, source);
            InstallZipPaths.CopyZipEntry(entry, destinationPath);
        }

        return new InstallPackageWorkspace(Path.Combine(temporaryDirectory, "manifest.json"), temporaryDirectory);
    }

    public void Dispose()
    {
        if (_temporaryDirectory is null || !Directory.Exists(_temporaryDirectory))
        {
            return;
        }

        Directory.Delete(_temporaryDirectory, recursive: true);
    }
}

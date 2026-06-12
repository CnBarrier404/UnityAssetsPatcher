using System.IO.Compression;
using UnityAssetsPatcher.Application.Contracts;

namespace UnityAssetsPatcher.Application.Modules;

public sealed class PackageWorkspace : IDisposable
{
    private readonly string? _temporaryDirectory;

    private PackageWorkspace(string configPath, string? temporaryDirectory)
    {
        ConfigPath = configPath;
        _temporaryDirectory = temporaryDirectory;
    }

    public string ConfigPath { get; }

    public static PackageWorkspace Create(string packagePath, ModManifest manifest)
    {
        string[] replacementSources = manifest.Patches
            .Select(patch => patch.ReplaceFrom?.AssetsFilePath)
            .OfType<string>()
            .Select(PackageArchive.NormalizeEntryPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (replacementSources.Length == 0)
        {
            return new PackageWorkspace(packagePath, null);
        }

        string temporaryDirectory = Path.Combine(Path.GetTempPath(), $"UnityAssetsPatcher.{Guid.NewGuid():N}");

        using ZipArchive archive = ZipFile.OpenRead(packagePath);

        foreach (string source in replacementSources)
        {
            ZipArchiveEntry entry = PackageArchive.FindFileEntry(archive, source, packagePath);
            string destinationPath = PackageArchive.ResolveUnderDirectory(temporaryDirectory, source);
            PackageArchive.CopyEntryToNewFile(entry, destinationPath);
        }

        return new PackageWorkspace(Path.Combine(temporaryDirectory, "manifest.json"), temporaryDirectory);
    }

    public void Dispose()
    {
        if (_temporaryDirectory is not null && Directory.Exists(_temporaryDirectory))
        {
            Directory.Delete(_temporaryDirectory, recursive: true);
        }
    }
}

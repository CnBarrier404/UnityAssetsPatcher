using System.IO.Compression;
using UnityAssetsPatcher.Application.Contracts;

namespace UnityAssetsPatcher.Application.Installing;

public sealed class InstallPayloadPlanner
{
    public IReadOnlyList<InstallCopyFilePlan> CreatePlans(
        string zipFilePath,
        IReadOnlyList<ManifestFile> files,
        IEnumerable<string> targetAssetsFilePaths,
        bool requireAvailableDestination)
    {
        if (files.Count == 0)
        {
            return [];
        }

        string targetDirectory = ResolveInstallPayloadDirectory(targetAssetsFilePaths);
        var plans = new List<InstallCopyFilePlan>();

        using ZipArchive archive = ZipFile.OpenRead(zipFilePath);

        foreach (ManifestFile file in files)
        {
            string source = InstallZipPaths.NormalizeZipEntryPath(file.Source);
            _ = InstallZipPaths.FindRequiredZipEntry(archive, source, zipFilePath);
            string destinationPath = Path.Combine(targetDirectory, InstallZipPaths.GetPayloadFileName(source));

            if (requireAvailableDestination && File.Exists(destinationPath))
            {
                throw new IOException($"Install payload file already exists: {destinationPath}");
            }

            plans.Add(new InstallCopyFilePlan(source, destinationPath));
        }

        return plans;
    }

    public IReadOnlyList<InstallCopiedFileResult> CopyFiles(
        string zipFilePath,
        IReadOnlyList<InstallCopyFilePlan> plans)
    {
        if (plans.Count == 0)
        {
            return [];
        }

        var results = new List<InstallCopiedFileResult>();

        using ZipArchive archive = ZipFile.OpenRead(zipFilePath);

        foreach (InstallCopyFilePlan plan in plans)
        {
            ZipArchiveEntry entry = InstallZipPaths.FindRequiredZipEntry(archive, plan.Source, zipFilePath);
            InstallZipPaths.CopyZipEntry(entry, plan.DestinationPath);
            results.Add(new InstallCopiedFileResult(plan.Source, plan.DestinationPath));
        }

        return results;
    }

    public IReadOnlyList<InstallCopyFilePreviewResult> CreatePreviewResults(
        IReadOnlyList<InstallCopyFilePlan> plans)
    {
        return plans
            .Select(plan => new InstallCopyFilePreviewResult(plan.Source, plan.DestinationPath,
                !File.Exists(plan.DestinationPath)))
            .ToArray();
    }

    private static string ResolveInstallPayloadDirectory(IEnumerable<string> targetAssetsFilePaths)
    {
        string[] targetDirectories = targetAssetsFilePaths
            .Select(path => Path.GetDirectoryName(Path.GetFullPath(path)) ??
                            throw new InvalidOperationException(
                                $"Cannot resolve directory for assets file: {path}"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return targetDirectories.Length switch
        {
            1 => targetDirectories[0],
            0 => throw new InvalidOperationException("Install payload files require at least one patch target."),
            _ => throw new InvalidOperationException(
                "Install payload files require all patch targets to resolve to the same directory.")
        };
    }
}

public sealed record InstallCopyFilePlan(string Source, string DestinationPath);

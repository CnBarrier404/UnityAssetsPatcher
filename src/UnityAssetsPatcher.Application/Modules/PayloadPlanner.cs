using System.IO.Compression;
using UnityAssetsPatcher.Application.Contracts;

namespace UnityAssetsPatcher.Application.Modules;

public sealed class PayloadPlanner
{
    public PayloadPlan Plan(PackageSource source, TargetAssetSet targets, bool requireAvailableDestination)
    {
        if (source.Manifest.Files.Count == 0)
        {
            return new PayloadPlan(source.PackagePath, source.Archive, []);
        }

        string payloadDirectory = ResolvePayloadDirectory(targets.AssetsFilePaths);
        var files = new List<PayloadFilePlan>();

        foreach (ManifestFile file in source.Manifest.Files)
        {
            string entryPath = PackageArchive.NormalizeEntryPath(file.Source);
            _ = PackageArchive.FindFileEntry(source.Archive, entryPath, source.PackagePath);
            string destinationPath = Path.Combine(payloadDirectory, PackageArchive.GetFileName(entryPath));

            if (requireAvailableDestination && File.Exists(destinationPath))
            {
                throw new IOException($"Payload file already exists: {destinationPath}");
            }

            files.Add(new PayloadFilePlan(entryPath, destinationPath));
        }

        return new PayloadPlan(source.PackagePath, source.Archive, files);
    }

    public static PayloadPreview Preview(PayloadPlan plan)
    {
        var files = plan.Files
            .Select(file => new PayloadFilePreview(
                file.Source,
                file.DestinationPath,
                !File.Exists(file.DestinationPath)))
            .ToArray();

        return new PayloadPreview(files);
    }

    private static string ResolvePayloadDirectory(IEnumerable<string> targetAssetsFilePaths)
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
            0 => throw new InvalidOperationException("Payload files require at least one patch target."),
            _ => throw new InvalidOperationException(
                "Payload files require all patch targets to resolve to the same directory.")
        };
    }
}

public sealed record PayloadPlan(
    string PackagePath,
    ZipArchive PackageArchive,
    IReadOnlyList<PayloadFilePlan> Files);

public sealed record PayloadFilePlan(string Source, string DestinationPath);

public sealed record PayloadPreview(IReadOnlyList<PayloadFilePreview> Files);

public sealed record PayloadFilePreview(string Source, string DestinationPath, bool WillCopy);

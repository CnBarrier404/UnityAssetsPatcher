using UnityAssetsPatcher.Application.Contracts;

namespace UnityAssetsPatcher.Application.Modules.Installation;

public sealed class InstallPayloadPlanner
{
    public IReadOnlyList<InstallPayloadFilePlan> Plan(ModManifest manifest, TargetAssetSet targets)
    {
        if (manifest.Files.Count == 0)
        {
            return [];
        }

        string payloadDirectory = ResolvePayloadDirectory(targets.AssetsFilePaths);
        var files = new List<InstallPayloadFilePlan>();

        foreach (ManifestFile file in manifest.Files)
        {
            string entryPath = file.Source.Replace('\\', '/');
            string fileName = Path.GetFileName(entryPath.Replace('/', Path.DirectorySeparatorChar));

            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new InvalidOperationException($"Payload source must name a file: {entryPath}");
            }

            files.Add(new InstallPayloadFilePlan(entryPath, Path.Combine(payloadDirectory, fileName)));
        }

        return files;
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

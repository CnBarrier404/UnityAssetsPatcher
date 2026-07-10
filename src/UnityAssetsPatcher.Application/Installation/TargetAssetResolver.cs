using UnityAssetsPatcher.Application.Contracts;

namespace UnityAssetsPatcher.Application.Installation;

public sealed class TargetAssetResolver
{
    public TargetAssetSet Execute(string gameDirectory, ModManifest manifest, StepTimer timings)
    {
        var targetPaths = timings.Measure("find-game-files", () => ResolveTargetPaths(
            gameDirectory,
            manifest.Patches.Select(patch => patch.AssetsFileName)));

        var targets = manifest.Patches
            .GroupBy(patch => patch.AssetsFileName, StringComparer.OrdinalIgnoreCase)
            .Select(group => new TargetAsset(group.Key, targetPaths[group.Key], group.ToArray()))
            .ToArray();

        return new TargetAssetSet(targets);
    }

    public static Dictionary<string, string> ResolveTargetPaths(
        string gameDirectory,
        IEnumerable<string> targetNames)
    {
        string fullGameDirectory = GetResolvedPath(gameDirectory);

        if (!Directory.Exists(fullGameDirectory))
        {
            throw new DirectoryNotFoundException($"Game directory not found: {fullGameDirectory}");
        }

        string[] distinctTargetNames = targetNames.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var matchesByTarget = distinctTargetNames.ToDictionary(
            target => target,
            _ => new List<string>(),
            StringComparer.OrdinalIgnoreCase);
        var targetNameSet = distinctTargetNames.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var enumerationOptions = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            AttributesToSkip = FileAttributes.ReparsePoint
        };

        foreach (string filePath in Directory.EnumerateFiles(fullGameDirectory, "*", enumerationOptions))
        {
            string fileName = Path.GetFileName(filePath);

            if (targetNameSet.Contains(fileName))
            {
                string resolvedFilePath = GetResolvedPath(filePath);

                if (!IsPathInsideDirectory(resolvedFilePath, fullGameDirectory))
                {
                    throw new InvalidOperationException(
                        $"Target '{fileName}' resolved outside game directory: {filePath}");
                }

                matchesByTarget[fileName].Add(resolvedFilePath);
            }
        }

        var resolvedTargets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (string targetName in distinctTargetNames)
        {
            var matches = matchesByTarget[targetName];

            switch (matches.Count)
            {
                case 0:
                    throw new FileNotFoundException(
                        $"Target '{targetName}' was not found under game directory: {fullGameDirectory}",
                        targetName);
                case > 1:
                    throw new InvalidOperationException(
                        $"Target '{targetName}' matched multiple files under game directory: {fullGameDirectory}");
                default:
                    resolvedTargets.Add(targetName, matches[0]);
                    break;
            }
        }

        return resolvedTargets;
    }

    public static string GetResolvedPath(string path)
    {
        string fullPath = Path.GetFullPath(path);
        string root = Path.GetPathRoot(fullPath) ??
                      throw new InvalidOperationException($"Cannot resolve path: {path}");
        string resolvedPath = root;
        string[] segments = fullPath[root.Length..]
            .Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                StringSplitOptions.RemoveEmptyEntries);

        foreach (string segment in segments)
        {
            resolvedPath = Path.Combine(resolvedPath, segment);
            FileSystemInfo? entry = GetFileSystemInfo(resolvedPath);

            if (entry?.LinkTarget is not null)
            {
                resolvedPath = entry.ResolveLinkTarget(returnFinalTarget: true)?.FullName ??
                               throw new InvalidOperationException($"Cannot resolve path: {path}");
            }
        }

        return Path.GetFullPath(resolvedPath);
    }

    public static bool IsPathInsideDirectory(string fullPath, string fullDirectory)
    {
        string directory = fullDirectory.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

        return fullPath.StartsWith(directory, PathComparison);
    }

    public static FileSystemInfo? GetFileSystemInfo(string path)
    {
        if (Directory.Exists(path))
        {
            return new DirectoryInfo(path);
        }

        return File.Exists(path) ? new FileInfo(path) : null;
    }

    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
}

public sealed record TargetAsset(string Name, string AssetsFilePath, IReadOnlyList<ManifestPatch> Patches);

public sealed record TargetAssetSet(IReadOnlyList<TargetAsset> Targets)
{
    public IReadOnlyList<string> AssetsFilePaths { get; } = Targets
        .Select(target => target.AssetsFilePath)
        .ToArray();
}

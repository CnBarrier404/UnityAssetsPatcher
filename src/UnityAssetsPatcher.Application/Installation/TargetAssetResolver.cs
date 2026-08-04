using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Application.Manifests;

namespace UnityAssetsPatcher.Application.Installation;

public sealed class TargetAssetResolver
{
    private readonly IFileSystemOperations _fileSystemOperations;

    public TargetAssetResolver(IFileSystemOperations fileSystemOperations)
    {
        ArgumentNullException.ThrowIfNull(fileSystemOperations);
        _fileSystemOperations = fileSystemOperations;
    }

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

    public Dictionary<string, string> ResolveTargetPaths(
        string gameDirectory,
        IEnumerable<string> targetNames)
    {
        string fullGameDirectory = _fileSystemOperations.ResolveExistingDirectory(gameDirectory);

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

            if (!targetNameSet.Contains(fileName))
            {
                continue;
            }

            string resolvedFilePath = _fileSystemOperations.ResolveExistingFile(filePath);

            if (!_fileSystemOperations.IsPathWithinDirectory(resolvedFilePath, fullGameDirectory))
            {
                throw new InvalidOperationException(
                    $"Target '{fileName}' resolved outside game directory: {filePath}");
            }

            matchesByTarget[fileName].Add(resolvedFilePath);
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
}

public sealed record TargetAsset(string Name, string AssetsFilePath, IReadOnlyList<ModPatch> Patches);

public sealed record TargetAssetSet(IReadOnlyList<TargetAsset> Targets)
{
    public IReadOnlyList<string> AssetsFilePaths { get; } =
    [
        .. Targets
            .Select(target => target.AssetsFilePath)
    ];
}

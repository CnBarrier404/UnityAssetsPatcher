using UnityAssetsPatcher.Application.Contracts;

namespace UnityAssetsPatcher.Application.Modules;

public sealed class TargetAssetResolver
{
    public TargetAssetSet Execute(string gameDirectory, ModManifest manifest, WorkflowTiming timings)
    {
        var targetPaths = timings.MeasureFindGameFiles(() => ResolveTargetPaths(
            gameDirectory,
            manifest.Patches.Select(patch => patch.AssetsFileName)));

        var targets = manifest.Patches
            .GroupBy(patch => patch.AssetsFileName, StringComparer.OrdinalIgnoreCase)
            .Select(group => new TargetAsset(group.Key, targetPaths[group.Key], group.ToArray()))
            .ToArray();

        return new TargetAssetSet(targets);
    }

    private static Dictionary<string, string> ResolveTargetPaths(
        string gameDirectory,
        IEnumerable<string> targetNames)
    {
        string fullGameDirectory = Path.GetFullPath(gameDirectory);
        string[] distinctTargetNames = targetNames.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var matchesByTarget = distinctTargetNames.ToDictionary(
            target => target,
            _ => new List<string>(),
            StringComparer.OrdinalIgnoreCase);
        var targetNameSet = distinctTargetNames.ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (string filePath in Directory.EnumerateFiles(fullGameDirectory, "*", SearchOption.AllDirectories))
        {
            string fileName = Path.GetFileName(filePath);

            if (targetNameSet.Contains(fileName))
            {
                matchesByTarget[fileName].Add(Path.GetFullPath(filePath));
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
}

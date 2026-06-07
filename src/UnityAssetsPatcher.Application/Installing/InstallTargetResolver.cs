namespace UnityAssetsPatcher.Application.Installing;

public static class InstallTargetResolver
{
    public static IReadOnlyDictionary<string, string> Resolve(
        string gameDirectory,
        IEnumerable<string> targets)
    {
        string fullGameDirectory = Path.GetFullPath(gameDirectory);

        return Resolve(
            fullGameDirectory,
            targets,
            Directory.EnumerateFiles(fullGameDirectory, "*", SearchOption.AllDirectories));
    }

    public static IReadOnlyDictionary<string, string> Resolve(
        string gameDirectory,
        IEnumerable<string> targets,
        IEnumerable<string> files)
    {
        string fullGameDirectory = Path.GetFullPath(gameDirectory);
        string[] distinctTargets = targets
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var resolvedTargets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var targetNames = distinctTargets.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var matchesByTarget = distinctTargets.ToDictionary(
            target => target,
            _ => new List<string>(),
            StringComparer.OrdinalIgnoreCase);

        foreach (string file in files)
        {
            string fileName = Path.GetFileName(file);

            if (!targetNames.Contains(fileName))
            {
                continue;
            }

            matchesByTarget[fileName].Add(Path.GetFullPath(file));
        }

        foreach (string target in distinctTargets)
        {
            var matches = matchesByTarget[target];

            switch (matches.Count)
            {
                case 0:
                    throw new FileNotFoundException(
                        $"Target '{target}' was not found under game directory: {fullGameDirectory}",
                        target);
                case > 1:
                    throw new InvalidOperationException(
                        $"Target '{target}' matched multiple files under game directory: {fullGameDirectory}");
                default:
                    resolvedTargets.Add(target, matches[0]);
                    break;
            }
        }

        return resolvedTargets;
    }
}

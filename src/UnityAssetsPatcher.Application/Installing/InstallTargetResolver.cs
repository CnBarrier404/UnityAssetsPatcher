namespace UnityAssetsPatcher.Application.Installing;

public static class InstallTargetResolver
{
    public static IReadOnlyDictionary<string, string> Resolve(
        string gameDirectory,
        IEnumerable<string> targets)
    {
        string fullGameDirectory = Path.GetFullPath(gameDirectory);
        var resolvedTargets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (string target in targets.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            string[] matches = Directory.EnumerateFiles(fullGameDirectory, "*", SearchOption.AllDirectories)
                .Where(file => string.Equals(Path.GetFileName(file), target, StringComparison.OrdinalIgnoreCase))
                .Select(Path.GetFullPath)
                .ToArray();

            if (matches.Length == 0)
            {
                throw new FileNotFoundException(
                    $"Target '{target}' was not found under game directory: {fullGameDirectory}",
                    target);
            }

            if (matches.Length > 1)
            {
                throw new InvalidOperationException(
                    $"Target '{target}' matched multiple files under game directory: {fullGameDirectory}");
            }

            resolvedTargets.Add(target, matches[0]);
        }

        return resolvedTargets;
    }
}

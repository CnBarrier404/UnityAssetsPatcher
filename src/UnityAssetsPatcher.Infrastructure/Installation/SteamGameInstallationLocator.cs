using System.Security;
using System.Text.RegularExpressions;
using UnityAssetsPatcher.Application.Installation;

namespace UnityAssetsPatcher.Infrastructure.Installation;

public sealed class SteamGameInstallationLocator : IGameInstallationLocator
{
    private static readonly Regex VdfKeyValuePattern = new(
        "\"(?<key>[^\"]+)\"\\s+\"(?<value>(?:\\\\.|[^\"])*)\"",
        RegexOptions.CultureInvariant);

    private readonly IReadOnlyList<string> _steamRoots;

    public SteamGameInstallationLocator(SteamInstallationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _steamRoots = options.RootDirectories
            .Where(root => !string.IsNullOrWhiteSpace(root))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public IReadOnlyList<string> FindGameDirectories(string game)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(game);

        string[] matches = _steamRoots
            .Where(Directory.Exists)
            .SelectMany(FindSteamLibraryDirectories)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .SelectMany(libraryDirectory => FindSteamGameDirectories(libraryDirectory, game))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return matches;
    }

    private static IEnumerable<string> FindSteamLibraryDirectories(string steamRoot)
    {
        yield return Path.GetFullPath(steamRoot);

        string libraryFoldersPath = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");

        if (!File.Exists(libraryFoldersPath))
        {
            yield break;
        }

        foreach (string libraryPath in ReadVdfValues(libraryFoldersPath, "path"))
        {
            if (!string.IsNullOrWhiteSpace(libraryPath))
            {
                yield return Path.GetFullPath(libraryPath.Replace(@"\\", @"\", StringComparison.Ordinal));
            }
        }
    }

    private static IEnumerable<string> FindSteamGameDirectories(string libraryDirectory, string game)
    {
        string steamAppsDirectory = Path.Combine(libraryDirectory, "steamapps");

        if (!Directory.Exists(steamAppsDirectory))
        {
            yield break;
        }

        IEnumerable<string> manifestPaths;

        try
        {
            manifestPaths = Directory.GetFiles(steamAppsDirectory, "appmanifest_*.acf");
        }
        catch (IOException)
        {
            yield break;
        }
        catch (UnauthorizedAccessException)
        {
            yield break;
        }
        catch (SecurityException)
        {
            yield break;
        }

        foreach (string manifestPath in manifestPaths)
        {
            string? name = ReadVdfValues(manifestPath, "name").FirstOrDefault();

            if (!string.Equals(name, game, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string? installDirectory = ReadVdfValues(manifestPath, "installdir").FirstOrDefault();

            if (string.IsNullOrWhiteSpace(installDirectory))
            {
                continue;
            }

            string gameDirectory = Path.GetFullPath(Path.Combine(
                steamAppsDirectory,
                "common",
                installDirectory));

            if (Directory.Exists(gameDirectory))
            {
                yield return gameDirectory;
            }
        }
    }

    private static IEnumerable<string> ReadVdfValues(string path, string key)
    {
        string[] lines;
        try
        {
            lines = File.ReadAllLines(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SecurityException)
        {
            // Discovery can continue through other manifests and Steam libraries.
            return [];
        }

        return from line in lines
            select VdfKeyValuePattern.Match(line)
            into match
            where match.Success &&
                  string.Equals(match.Groups["key"].Value, key, StringComparison.OrdinalIgnoreCase)
            select match.Groups["value"].Value;
    }
}

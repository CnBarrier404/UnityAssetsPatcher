using System.Text.RegularExpressions;

namespace UnityAssetsPatcher.Application.Modules;

public sealed class GameDirectoryResolver
{
    private static readonly Regex VdfKeyValuePattern = new(
        "\"(?<key>[^\"]+)\"\\s+\"(?<value>(?:\\\\.|[^\"])*)\"",
        RegexOptions.CultureInvariant);

    private readonly IReadOnlyList<string> _steamRoots;

    public GameDirectoryResolver() : this(GetDefaultSteamRoots()) { }

    public GameDirectoryResolver(IEnumerable<string> steamRoots)
    {
        _steamRoots = steamRoots
            .Where(root => !string.IsNullOrWhiteSpace(root))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public string? Resolve(string game)
    {
        if (string.IsNullOrWhiteSpace(game))
        {
            return null;
        }

        string[] matches = _steamRoots
            .Where(Directory.Exists)
            .SelectMany(FindSteamLibraryDirectories)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .SelectMany(libraryDirectory => FindSteamGameDirectories(libraryDirectory, game))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return matches.Length == 1 ? matches[0] : null;
    }

    public static string[] CreateDefaultSteamRoots(IEnumerable<string> driveRoots)
    {
        var roots = new List<string>();
        AddIfNotNull(roots, Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            "Steam"));

        foreach (string driveRoot in driveRoots)
        {
            AddIfNotNull(roots, Path.Combine(driveRoot, "Steam"));
            AddIfNotNull(roots, Path.Combine(driveRoot, "SteamLibrary"));
        }

        return roots
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string[] GetDefaultSteamRoots()
    {
        return CreateDefaultSteamRoots(DriveInfo.GetDrives()
            .Where(drive => drive.IsReady)
            .Select(drive => drive.RootDirectory.FullName));
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
            manifestPaths = Directory.EnumerateFiles(steamAppsDirectory, "appmanifest_*.acf");
        }
        catch (IOException)
        {
            yield break;
        }
        catch (UnauthorizedAccessException)
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
        return from line in File.ReadLines(path)
            select VdfKeyValuePattern.Match(line)
            into match
            where match.Success &&
                  string.Equals(match.Groups["key"].Value, key, StringComparison.OrdinalIgnoreCase)
            select match.Groups["value"].Value;
    }

    private static void AddIfNotNull(List<string> roots, string? root)
    {
        if (!string.IsNullOrWhiteSpace(root))
        {
            roots.Add(root);
        }
    }
}

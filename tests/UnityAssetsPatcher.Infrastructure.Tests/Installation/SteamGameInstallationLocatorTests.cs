using UnityAssetsPatcher.Infrastructure.Installation;
using Xunit;

namespace UnityAssetsPatcher.Infrastructure.Tests.Installation;

public sealed class SteamGameInstallationLocatorTests : IDisposable
{
    private readonly string _temporaryDirectory = Path.Combine(
        Path.GetTempPath(),
        $"UnityAssetsPatcher-Steam-{Guid.NewGuid():N}");

    [Fact]
    public void FindGameDirectories_WhenGameIsInstalledInSteamRoot_ReturnsInstallationDirectory()
    {
        string steamRoot = Path.Combine(_temporaryDirectory, "Steam");
        string gameDirectory = CreateInstalledGame(steamRoot, "739630", "Phasmophobia", "Phasmophobia");
        var locator = new SteamGameInstallationLocator(new SteamInstallationOptions([steamRoot]));

        IReadOnlyList<string> result = locator.FindGameDirectories("Phasmophobia");

        Assert.Equal([Path.GetFullPath(gameDirectory)], result);
    }

    [Fact]
    public void FindGameDirectories_WhenGameIsInstalledInConfiguredLibrary_ReturnsInstallationDirectory()
    {
        string steamRoot = Path.Combine(_temporaryDirectory, "Steam");
        string libraryRoot = Path.Combine(_temporaryDirectory, "Steam Library");
        string libraryFoldersPath = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
        Directory.CreateDirectory(Path.GetDirectoryName(libraryFoldersPath)!);
        File.WriteAllText(
            libraryFoldersPath,
            $$"""
              "libraryfolders"
              {
                  "0"
                  {
                      "path" "{{EscapeVdfPath(libraryRoot)}}"
                  }
              }
              """);
        string gameDirectory = CreateInstalledGame(libraryRoot, "739630", "Phasmophobia", "Phasmophobia");
        var locator = new SteamGameInstallationLocator(new SteamInstallationOptions([steamRoot]));

        IReadOnlyList<string> result = locator.FindGameDirectories("Phasmophobia");

        Assert.Equal([Path.GetFullPath(gameDirectory)], result);
    }

    [Fact]
    public void FindGameDirectories_WhenGameNameDoesNotMatch_ReturnsEmptyCollection()
    {
        string steamRoot = Path.Combine(_temporaryDirectory, "Steam");
        _ = CreateInstalledGame(steamRoot, "739630", "Phasmophobia", "Phasmophobia");
        var locator = new SteamGameInstallationLocator(new SteamInstallationOptions([steamRoot]));

        IReadOnlyList<string> result = locator.FindGameDirectories("Another Game");

        Assert.Empty(result);
    }

    public void Dispose()
    {
        if (Directory.Exists(_temporaryDirectory))
        {
            Directory.Delete(_temporaryDirectory, recursive: true);
        }
    }

    private static string CreateInstalledGame(string steamRoot, string appId, string name, string installDirectory)
    {
        string steamAppsDirectory = Path.Combine(steamRoot, "steamapps");
        string gameDirectory = Path.Combine(steamAppsDirectory, "common", installDirectory);
        Directory.CreateDirectory(gameDirectory);
        File.WriteAllText(
            Path.Combine(steamAppsDirectory, $"appmanifest_{appId}.acf"),
            $$"""
              "AppState"
              {
                  "appid" "{{appId}}"
                  "name" "{{name}}"
                  "installdir" "{{installDirectory}}"
              }
              """);

        return gameDirectory;
    }

    private static string EscapeVdfPath(string path)
    {
        return path.Replace(@"\", @"\\", StringComparison.Ordinal);
    }
}

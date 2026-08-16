using Microsoft.Extensions.DependencyInjection;
using UnityAssetsPatcher.Application;
using UnityAssetsPatcher.Application.Installation;
using UnityAssetsPatcher.Infrastructure.Installation;
using Xunit;

namespace UnityAssetsPatcher.Infrastructure.Tests.Installation;

public sealed class GameInstallationDependencyInjectionTests : IDisposable
{
    private readonly string _temporaryDirectory = Path.Combine(
        Path.GetTempPath(),
        $"UnityAssetsPatcher-GameDiscovery-{Guid.NewGuid():N}");

    [Fact]
    public void AddInfrastructure_WhenProviderValidationIsEnabled_ResolvesGameDirectoryResolver()
    {
        string steamRoot = Path.Combine(_temporaryDirectory, "Steam");
        string gameDirectory = CreateInstalledGame(steamRoot);
        string repositoryDirectory = Path.Combine(_temporaryDirectory, "Backup");
        var services = new ServiceCollection();

        services.AddLogging();

        services.AddSingleton(new SteamInstallationOptions([steamRoot]));

        services.AddUnityAssetsPatcherInfrastructure(OpenClassPackage);

        services.AddUnityAssetsPatcherRepository(repositoryDirectory);

        services.AddUnityAssetsPatcherApplication();

        services.AddUnityAssetsPatcherOperations();

        using ServiceProvider provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        var resolver = provider.GetRequiredService<GameDirectoryResolver>();

        string? result = resolver.Resolve("Phasmophobia");

        Assert.Equal(Path.GetFullPath(gameDirectory), result);
        Assert.IsType<SteamGameInstallationLocator>(provider.GetRequiredService<IGameInstallationLocator>());
    }

    public void Dispose()
    {
        if (Directory.Exists(_temporaryDirectory))
        {
            Directory.Delete(_temporaryDirectory, true);
        }
    }

    private static string CreateInstalledGame(string steamRoot)
    {
        string steamAppsDirectory = Path.Combine(steamRoot, "steamapps");
        string gameDirectory = Path.Combine(steamAppsDirectory, "common", "Phasmophobia");
        Directory.CreateDirectory(gameDirectory);
        File.WriteAllText(
            Path.Combine(steamAppsDirectory, "appmanifest_739630.acf"),
            """
            "AppState"
            {
                "appid" "739630"
                "name" "Phasmophobia"
                "installdir" "Phasmophobia"
            }
            """);

        return gameDirectory;
    }

    private static Stream OpenClassPackage()
    {
        return File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures", "resources.tpk"));
    }
}

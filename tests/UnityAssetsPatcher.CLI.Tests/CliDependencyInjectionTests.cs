using Microsoft.Extensions.DependencyInjection;
using UnityAssetsPatcher.Application;
using UnityAssetsPatcher.Infrastructure;
using Xunit;

namespace UnityAssetsPatcher.CLI.Tests;

public sealed class CliDependencyInjectionTests
{
    [Fact]
    public void AddUnityAssetsPatcherCli_WhenProviderValidationIsEnabled_RegistersApplication()
    {
        var services = new ServiceCollection();

        services.AddLogging();

        services.AddUnityAssetsPatcherPackageHandling();

        services.AddUnityAssetsPatcherApplication();

        services.AddUnityAssetsPatcherCli();

        using ServiceProvider provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });

        var application = provider.GetRequiredService<CLIApplication>();

        Assert.NotNull(application);
    }
}

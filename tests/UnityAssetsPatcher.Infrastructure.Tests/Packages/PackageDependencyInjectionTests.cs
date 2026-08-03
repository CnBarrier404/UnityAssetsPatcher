using Microsoft.Extensions.DependencyInjection;
using UnityAssetsPatcher.Application;
using UnityAssetsPatcher.Application.Manifests;
using UnityAssetsPatcher.Application.Packages;
using UnityAssetsPatcher.Application.Workflows;
using Xunit;

namespace UnityAssetsPatcher.Infrastructure.Tests.Packages;

public sealed class PackageDependencyInjectionTests
{
    [Fact]
    public void AddPackageHandling_WhenProviderValidationIsEnabled_ResolvesApplicationPackageServices()
    {
        var services = new ServiceCollection();

        services.AddLogging();

        services.AddUnityAssetsPatcherPackageHandling();

        services.AddUnityAssetsPatcherApplication();

        using ServiceProvider provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });

        ModPackageArchiveService archiveService = provider.GetRequiredService<ModPackageArchiveService>();

        ManifestSourceReader sourceReader = provider.GetRequiredService<ManifestSourceReader>();

        CheckManifestWorkflow checkWorkflow = provider.GetRequiredService<CheckManifestWorkflow>();

        Assert.NotNull(archiveService);
        Assert.NotNull(sourceReader);
        Assert.NotNull(checkWorkflow);
    }
}

using Microsoft.Extensions.DependencyInjection;
using UnityAssetsPatcher.Application;
using UnityAssetsPatcher.Application.Features.Check;
using UnityAssetsPatcher.Application.Manifests;
using UnityAssetsPatcher.Application.Messaging;
using UnityAssetsPatcher.Application.Packages;
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
        IModManifestService manifestService = provider.GetRequiredService<IModManifestService>();

        using IServiceScope scope = provider.CreateScope();
        IRequestHandler<CheckManifestRequest, CheckManifestResult> checkHandler =
            scope.ServiceProvider.GetRequiredService<
                IRequestHandler<CheckManifestRequest, CheckManifestResult>>();

        Assert.NotNull(archiveService);
        Assert.NotNull(sourceReader);
        Assert.NotNull(manifestService);
        Assert.NotNull(checkHandler);
    }
}

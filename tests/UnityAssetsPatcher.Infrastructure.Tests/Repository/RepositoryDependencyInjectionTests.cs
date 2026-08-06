using Microsoft.Extensions.DependencyInjection;
using UnityAssetsPatcher.Application.Repository;
using Xunit;

namespace UnityAssetsPatcher.Infrastructure.Tests.Repository;

public sealed class RepositoryDependencyInjectionTests
{
    [Fact]
    public void AddRepository_WhenProviderValidationIsEnabled_RegistersRepositoryService()
    {
        using RepositoryTestDirectory directory = new();
        var services = new ServiceCollection();

        services.AddLogging();

        services.AddUnityAssetsPatcherRepository(directory.GetPath("backup"));

        using ServiceProvider provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });

        IRepository repository = provider.GetRequiredService<IRepository>();

        Assert.NotNull(repository);
    }
}

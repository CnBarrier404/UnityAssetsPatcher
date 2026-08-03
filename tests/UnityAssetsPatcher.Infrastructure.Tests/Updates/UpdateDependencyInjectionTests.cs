using Microsoft.Extensions.DependencyInjection;
using UnityAssetsPatcher.Application;
using UnityAssetsPatcher.Application.Updates;
using Xunit;

namespace UnityAssetsPatcher.Infrastructure.Tests.Updates;

public sealed class UpdateDependencyInjectionTests
{
    [Fact]
    public void AddUnityAssetsPatcherUpdateChecking_WhenProviderValidationIsEnabled_RegistersUpdateChecker()
    {
        var services = new ServiceCollection();

        services.AddSingleton(new AppInfo("Unity Assets Patcher", "dev"));

        services.AddLogging();

        services.AddUnityAssetsPatcherUpdateChecking();

        using ServiceProvider provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });

        IUpdateChecker checker = provider.GetRequiredService<IUpdateChecker>();

        Assert.NotNull(checker);
    }
}

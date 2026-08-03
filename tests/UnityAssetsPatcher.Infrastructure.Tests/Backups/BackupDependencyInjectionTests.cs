using Microsoft.Extensions.DependencyInjection;
using UnityAssetsPatcher.Application.Backups;
using UnityAssetsPatcher.Infrastructure;
using Xunit;

namespace UnityAssetsPatcher.Infrastructure.Tests.Backups;

public sealed class BackupDependencyInjectionTests
{
    [Fact]
    public void AddBackupRepository_WhenProviderValidationIsEnabled_RegistersBackupRepository()
    {
        using BackupTestDirectory directory = new();
        var services = new ServiceCollection();

        services.AddLogging();

        services.AddUnityAssetsPatcherBackupRepository(directory.GetPath("backup"));

        using ServiceProvider provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });

        IBackupRepository repository = provider.GetRequiredService<IBackupRepository>();

        Assert.NotNull(repository);
    }
}

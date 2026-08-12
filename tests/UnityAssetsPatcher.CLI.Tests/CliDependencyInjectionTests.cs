using Microsoft.Extensions.DependencyInjection;
using UnityAssetsPatcher.Application;
using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Application.Mods;
using UnityAssetsPatcher.Infrastructure.IO;
using UnityAssetsPatcher.Infrastructure.Mods;
using Xunit;

namespace UnityAssetsPatcher.CLI.Tests;

public sealed class CliDependencyInjectionTests
{
    [Fact]
    public void AddUnityAssetsPatcherCli_WhenProviderValidationIsEnabled_RegistersApplication()
    {
        var services = new ServiceCollection();

        services.AddLogging();

        services.AddSingleton<IFileSystemOperations>(provider => new FileSystemOperations(
            provider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<FileSystemOperations>>()));
        services.AddSingleton<IModArchiveReader, ZipModArchiveReader>();

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

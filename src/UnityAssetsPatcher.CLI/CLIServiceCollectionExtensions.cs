using Microsoft.Extensions.DependencyInjection;
using UnityAssetsPatcher.Application.Backups;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Manifests;

namespace UnityAssetsPatcher.CLI;

public static class CLIServiceCollectionExtensions
{
    public static IServiceCollection AddUnityAssetsPatcherCLI(this IServiceCollection services)
    {
        services.AddSingleton<CLIOptions>();
        services.AddSingleton<ICLICommand>(provider => new CheckCLICommand(
            provider.GetRequiredService<ModManifestReader>(),
            () => Environment.CurrentDirectory,
            provider.GetRequiredService<CLIOptions>()));
        services.AddSingleton<ICLICommand>(provider => new InstallCLICommand(
            provider.GetRequiredService<IWorkflowService>(),
            provider.GetRequiredService<ModBackupStore>(),
            provider.GetRequiredService<CLIOptions>()));
        services.AddSingleton<ICLICommand>(provider => new UninstallCLICommand(
            provider.GetRequiredService<IWorkflowService>(),
            provider.GetRequiredService<ModBackupStore>(),
            provider.GetRequiredService<CLIOptions>()));
        services.AddSingleton(provider => new CLIApplication(
            provider.GetServices<ICLICommand>(),
            Console.Out,
            Console.Error,
            provider.GetRequiredService<CLIOptions>()));

        return services;
    }
}

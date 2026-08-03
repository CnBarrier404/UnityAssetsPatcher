using Microsoft.Extensions.DependencyInjection;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Workflows;

namespace UnityAssetsPatcher.CLI;

public static class CLIServiceCollectionExtensions
{
    public static IServiceCollection AddUnityAssetsPatcherCli(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<ICLICommand>(provider => new CheckCLICommand(
            provider.GetRequiredService<CheckManifestWorkflow>(),
            () => Environment.CurrentDirectory,
            Console.Error));

        services.AddSingleton(provider => new CLIApplication(
            provider.GetServices<ICLICommand>(),
            Console.Out,
            Console.Error,
            provider.GetService<CLIOptions>()));

        return services;
    }

    public static IServiceCollection AddUnityAssetsPatcherOperationalCommands(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<CLIOptions>();

        services.AddSingleton<ICLICommand>(provider => new InspectCLICommand(
            provider.GetRequiredService<IWorkflowService>(),
            provider.GetRequiredService<CLIOptions>()));

        services.AddSingleton<ICLICommand>(provider => new InstallCLICommand(
            provider.GetRequiredService<IWorkflowService>(),
            provider.GetRequiredService<CLIOptions>()));

        services.AddSingleton<ICLICommand>(provider => new UninstallCLICommand(
            provider.GetRequiredService<IWorkflowService>(),
            provider.GetRequiredService<CLIOptions>()));

        services.AddSingleton<ICLICommand>(provider => new RecoveryCLICommand(
            provider.GetRequiredService<IWorkflowService>(),
            provider.GetRequiredService<CLIOptions>()));

        return services;
    }
}

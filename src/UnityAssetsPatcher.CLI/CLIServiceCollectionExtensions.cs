using Microsoft.Extensions.DependencyInjection;
using UnityAssetsPatcher.Application.Contracts;

namespace UnityAssetsPatcher.CLI;

public static class CLIServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddUnityAssetsPatcherCli()
        {
            ArgumentNullException.ThrowIfNull(services);

            services.AddSingleton<ICLICommand>(provider => new CheckCLICommand(
                provider.GetRequiredService<IServiceScopeFactory>(),
                () => Environment.CurrentDirectory,
                Console.Error));

            services.AddSingleton(provider => new CLIApplication(
                provider.GetServices<ICLICommand>(),
                Console.Out,
                Console.Error,
                provider.GetService<CLIOptions>()));

            return services;
        }

        public IServiceCollection AddUnityAssetsPatcherOperationalCommands()
        {
            ArgumentNullException.ThrowIfNull(services);

            services.AddSingleton<CLIOptions>();

            services.AddSingleton<ICLICommand>(provider => new InspectCLICommand(
                provider.GetRequiredService<IWorkflowService>(),
                provider.GetRequiredService<CLIOptions>()));

            services.AddSingleton<ICLICommand>(provider => new InstallCLICommand(
                provider.GetRequiredService<IServiceScopeFactory>(),
                provider.GetRequiredService<CLIOptions>()));

            services.AddSingleton<ICLICommand>(provider => new UninstallCLICommand(
                provider.GetRequiredService<IServiceScopeFactory>(),
                provider.GetRequiredService<IWorkflowService>(),
                provider.GetRequiredService<CLIOptions>()));

            services.AddSingleton<ICLICommand>(provider => new RecoveryCLICommand(
                provider.GetRequiredService<IWorkflowService>(),
                provider.GetRequiredService<CLIOptions>()));

            return services;
        }
    }
}

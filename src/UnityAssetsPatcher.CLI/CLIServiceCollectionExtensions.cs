using Microsoft.Extensions.DependencyInjection;

namespace UnityAssetsPatcher.CLI;

public static class CLIServiceCollectionExtensions
{
    public static IServiceCollection AddUnityAssetsPatcherCLI(this IServiceCollection services)
    {
        services.AddSingleton<ICLICommand>(provider => new CheckCLICommand(
            provider.GetRequiredService<UnityAssetsPatcher.Application.Manifests.ModManifestReader>(),
            () => Environment.CurrentDirectory));
        services.AddSingleton(provider => new CLIApplication(
            provider.GetServices<ICLICommand>(),
            Console.Out,
            Console.Error));

        return services;
    }
}

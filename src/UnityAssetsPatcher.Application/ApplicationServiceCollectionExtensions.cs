using Microsoft.Extensions.DependencyInjection;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Workflows;
using UnityAssetsPatcher.Core.Assets;

namespace UnityAssetsPatcher.Application;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddUnityAssetsPatcherApplication(
        this IServiceCollection services,
        string backupDirectory)
    {
        services.AddSingleton<WorkflowFactory>(_ => new WorkflowFactory());
        services.AddSingleton<IWorkflowService>(provider => new WorkflowService(
            provider.GetRequiredService<IAssetsAccessScopeFactory>(),
            backupDirectory,
            provider.GetRequiredService<WorkflowFactory>()));

        return services;
    }
}

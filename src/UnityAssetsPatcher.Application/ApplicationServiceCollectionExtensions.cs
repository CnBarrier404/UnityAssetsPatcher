using Microsoft.Extensions.DependencyInjection;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Backups;
using UnityAssetsPatcher.Application.Installation;
using UnityAssetsPatcher.Application.Manifests;
using UnityAssetsPatcher.Application.Workflows;
using UnityAssetsPatcher.Core.Assets;

namespace UnityAssetsPatcher.Application;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddUnityAssetsPatcherApplication(
        this IServiceCollection services,
        string backupDirectory)
    {
        services.AddSingleton<ModManifestReader>();
        services.AddSingleton(_ => new GameDirectoryResolver());
        services.AddSingleton<TargetAssetResolver>();
        services.AddSingleton(new BackupRepository(backupDirectory));
        services.AddSingleton<WorkflowFactory>();
        services.AddSingleton<IWorkflowService>(provider => new WorkflowService(
            provider.GetRequiredService<IAssetsAccessScopeFactory>(),
            provider.GetRequiredService<WorkflowFactory>(),
            provider.GetRequiredService<ModManifestReader>(),
            provider.GetRequiredService<BackupRepository>()));

        return services;
    }
}

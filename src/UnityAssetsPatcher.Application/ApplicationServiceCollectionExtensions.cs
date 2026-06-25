using Microsoft.Extensions.DependencyInjection;
using System.IO.Compression;
using UnityAssetsPatcher.Application.Contracts;
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
        services.AddSingleton<Func<string, ZipArchive>>(_ => ZipFile.OpenRead);
        services.AddSingleton<TargetAssetResolver>();
        services.AddSingleton<ModInstallationStoreFactory>();
        services.AddSingleton<AssetQueryServiceFactory>();
        services.AddSingleton<PatchPlanBuilderFactory>();
        services.AddSingleton<PatchOutputWriterFactory>();
        services.AddSingleton<IInstallModWorkflowFactory, InstallModWorkflowFactory>();
        services.AddSingleton<IUninstallModWorkflowFactory, UninstallModWorkflowFactory>();
        services.AddSingleton<IWorkflowService>(provider => new WorkflowService(
            provider.GetRequiredService<IAssetsAccessScopeFactory>(),
            backupDirectory,
            provider.GetRequiredService<IInstallModWorkflowFactory>(),
            provider.GetRequiredService<IUninstallModWorkflowFactory>()));

        return services;
    }
}

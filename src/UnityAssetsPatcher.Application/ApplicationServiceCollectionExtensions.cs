using Microsoft.Extensions.DependencyInjection;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Manifests;
using UnityAssetsPatcher.Application.Modules;
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
        services.AddSingleton<GameDirectoryResolver>();
        services.AddSingleton<Func<string, System.IO.Compression.ZipArchive>>(_ => PackageArchive.OpenRead);
        services.AddSingleton<ManifestPatchOperationValidator>();
        services.AddSingleton<TargetAssetResolver>();
        services.AddSingleton<ModInstallationStoreFactory>();
        services.AddSingleton<AssetQueryServiceFactory>();
        services.AddSingleton<PatchPlanBuilderFactory>();
        services.AddSingleton<PatchOutputWriterFactory>();
        services.AddSingleton<PatchPlannerFactory>();
        services.AddSingleton<PatchAssetApplierFactory>();
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

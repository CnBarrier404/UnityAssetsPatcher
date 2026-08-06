using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UnityAssetsPatcher.Application.Repository;
using UnityAssetsPatcher.Application.Composition;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Installation;
using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Application.Manifests;
using UnityAssetsPatcher.Application.Packages;
using UnityAssetsPatcher.Application.Patching;
using UnityAssetsPatcher.Application.Patching.Fields;
using UnityAssetsPatcher.Application.Uninstallation;
using UnityAssetsPatcher.Application.Workflows;

namespace UnityAssetsPatcher.Application;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddUnityAssetsPatcherApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<ModPackageArchiveService>();
        services.AddSingleton<ManifestSourceReader>();
        services.AddSingleton<CheckManifestWorkflow>();

        return services;
    }

    public static IServiceCollection AddUnityAssetsPatcherOperations(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<GameDirectoryResolver>();
        services.AddSingleton<TargetAssetResolver>();
        services.AddSingleton(provider => new RepositoryService(
            provider.GetRequiredService<IRepository>(),
            provider.GetRequiredService<ICompositionRepository>(),
            provider.GetRequiredService<IFileSystemOperations>(),
            provider.GetService<ILogger<RepositoryService>>()));
        services.AddSingleton<BaseSnapshotCapturer>();
        services.AddSingleton<IRepositoryService>(provider => provider.GetRequiredService<RepositoryService>());
        services.AddSingleton<IWorkflowService, WorkflowService>();

        AddPatching(services);
        AddWorkflows(services);

        return services;
    }

    private static void AddPatching(IServiceCollection services)
    {
        services.AddScoped<IFieldPatchOperationHandler, SetFieldPatchOperationHandler>();
        services.AddScoped<IFieldPatchOperationHandler, AddFieldPatchOperationHandler>();
        services.AddScoped<PatchOutputWriter>();
        services.AddScoped<ModComposer>();
        services.AddScoped<UninstallCompositionService>();
    }

    private static void AddWorkflows(IServiceCollection services)
    {
        services.AddScoped<InstallPlanBuilder>();
        services.AddScoped<InstallExecutor>();
        services.AddScoped<InstallModWorkflow>();
        services.AddScoped<InspectAssetsWorkflow>();
        services.AddScoped<UninstallPlanner>();
        services.AddScoped<UninstallExecutor>();
        services.AddScoped<UninstallModWorkflow>();
    }
}

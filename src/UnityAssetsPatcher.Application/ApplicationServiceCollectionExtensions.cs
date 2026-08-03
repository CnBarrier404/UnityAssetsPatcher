using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UnityAssetsPatcher.Application.Backups;
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
        services.AddSingleton<TrustedPathResolver>();

        return services;
    }

    public static IServiceCollection AddUnityAssetsPatcherOperations(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<ModManifestReader>();
        services.AddSingleton<GameDirectoryResolver>();
        services.AddSingleton<TargetAssetResolver>();
        services.AddSingleton(provider => new BackupRepository(
            provider.GetRequiredService<IBackupRepository>(),
            provider.GetRequiredService<IFileSystemOperations>(),
            provider.GetService<ILogger<BackupRepository>>()));
        services.AddSingleton<IBackupService>(provider => provider.GetRequiredService<BackupRepository>());
        services.AddSingleton<IWorkflowService, WorkflowService>();

        AddPatching(services);
        AddWorkflows(services);

        return services;
    }

    private static void AddPatching(IServiceCollection services)
    {
        services.AddScoped<AssetQueryService>();
        services.AddScoped<IFieldPatchOperationHandler, SetFieldPatchOperationHandler>();
        services.AddScoped<IFieldPatchOperationHandler, AddFieldPatchOperationHandler>();
        services.AddScoped<FieldPatchPlanner>();
        services.AddScoped<ReplacementPlanner>();
        services.AddScoped<CopyAssetPlanner>();
        services.AddScoped<PatchPlanner>();
        services.AddScoped<PatchOutputWriter>();
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

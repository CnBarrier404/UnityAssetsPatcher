using Microsoft.Extensions.DependencyInjection;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Backups;
using UnityAssetsPatcher.Application.Installation;
using UnityAssetsPatcher.Application.Manifests;
using UnityAssetsPatcher.Application.Workflows;
using UnityAssetsPatcher.Application.Patching;
using UnityAssetsPatcher.Application.Patching.Fields;
using UnityAssetsPatcher.Application.Uninstallation;

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
        services.AddScoped<InstallPlanner>();
        services.AddScoped<InstallExecutor>();
        services.AddScoped<InstallModWorkflow>();
        services.AddScoped<InspectAssetsWorkflow>();
        services.AddScoped<UninstallPlanner>();
        services.AddScoped<UninstallExecutor>();
        services.AddScoped<UninstallModWorkflow>();
    }
}

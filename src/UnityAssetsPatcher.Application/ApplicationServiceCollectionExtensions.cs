using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UnityAssetsPatcher.Application.Repository;
using UnityAssetsPatcher.Application.Composition;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Features.Check;
using UnityAssetsPatcher.Application.Features.Install;
using UnityAssetsPatcher.Application.Features.Inspect;
using UnityAssetsPatcher.Application.Features.Recovery;
using UnityAssetsPatcher.Application.Features.RepositoryManagement;
using UnityAssetsPatcher.Application.Features.Uninstall;
using UnityAssetsPatcher.Application.Installation;
using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Application.Messaging;
using UnityAssetsPatcher.Application.Mods;
using UnityAssetsPatcher.Application.Operations;
using UnityAssetsPatcher.Application.Patching;
using UnityAssetsPatcher.Application.Patching.Fields;
using UnityAssetsPatcher.Application.Uninstallation;
using UnityAssetsPatcher.Domain.Assets;
using RepositoryFacade = UnityAssetsPatcher.Application.Repository.Repository;

namespace UnityAssetsPatcher.Application;

public static class ApplicationServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddUnityAssetsPatcherApplication()
        {
            ArgumentNullException.ThrowIfNull(services);

            services.AddScoped<IRequestDispatcher, RequestDispatcher>();
            services.AddSingleton<ModPackageReader>();
            services.AddScoped<ModManifestReader>();
            services.AddScoped<
                IRequestHandler<CheckManifestRequest, OperationResult<CheckManifestResult>>,
                CheckManifestHandler>();

            return services;
        }

        public IServiceCollection AddUnityAssetsPatcherOperations()
        {
            ArgumentNullException.ThrowIfNull(services);

            services.AddSingleton<GameDirectoryResolver>();
            services.AddSingleton<TargetAssetResolver>();
            services.AddSingleton(provider => new RepositoryService(
                provider.GetRequiredService<IRepositoryStore>(),
                provider.GetRequiredService<IFileSystemOperations>(),
                provider.GetRequiredService<IRepositoryOperationLockProvider>(),
                provider.GetService<ILogger<RepositoryService>>()));
            services.AddSingleton<BaseSnapshotCapturer>();
            services.AddScoped<IRepository, RepositoryFacade>();
            AddPatching(services);
            AddOperationServices(services);
            AddInspectHandlers(services);
            AddInstallHandlers(services);
            AddRecoveryHandlers(services);
            AddRepositoryManagementHandlers(services);
            AddUninstallHandlers(services);

            return services;
        }
    }

    private static void AddPatching(IServiceCollection services)
    {
        services.AddScoped<IFieldPatchOperationHandler, SetFieldPatchOperationHandler>();
        services.AddScoped<IFieldPatchOperationHandler, AddFieldPatchOperationHandler>();
        services.AddScoped<PatchOutputWriter>();
        services.AddScoped<ModComposer>();
        services.AddScoped<UninstallCompositionService>();
    }

    private static void AddOperationServices(IServiceCollection services)
    {
        services.AddScoped<InstallPlanBuilder>();
        services.AddScoped<InstallExecutor>();
        services.AddScoped<UninstallPlanner>();
        services.AddScoped<UninstallExecutor>();
    }

    private static void AddInspectHandlers(IServiceCollection services)
    {
        services.AddScoped<
            IRequestHandler<InspectListRequest, OperationResult<InspectListResult>>,
            InspectAssetsHandler>();
        services.AddScoped<
            IRequestHandler<InspectFieldsRequest, OperationResult<AssetField>>,
            InspectAssetsHandler>();
    }

    private static void AddInstallHandlers(IServiceCollection services)
    {
        services.AddScoped<
            IRequestHandler<PreviewInstallRequest, OperationResult<InstallPreviewResult>>,
            InstallModHandler>();
        services.AddScoped<
            IRequestHandler<InstallModRequest, OperationResult<InstallModResult>>,
            InstallModHandler>();
    }

    private static void AddRecoveryHandlers(IServiceCollection services)
    {
        services.AddScoped<
            IRequestHandler<PreviewRecoveryRequest, OperationResult<RepositoryRecoveryPreview>>,
            RecoveryHandler>();
        services.AddScoped<
            IRequestHandler<RecoverRecoveryRequest, OperationResult<RepositoryRecoveryReport>>,
            RecoveryHandler>();
    }

    private static void AddRepositoryManagementHandlers(IServiceCollection services)
    {
        services.AddScoped<
            IRequestHandler<ClearUnsupportedRepositoryRequest, OperationResult<RepositoryClearResult>>,
            RepositoryManagementHandler>();
    }

    private static void AddUninstallHandlers(IServiceCollection services)
    {
        services.AddScoped<
            IRequestHandler<UninstallPreviewRequest, OperationResult<UninstallPreviewResult>>,
            UninstallModHandler>();
        services.AddScoped<
            IRequestHandler<UninstallModRequest, OperationResult<UninstallModResult>>,
            UninstallModHandler>();
        services.AddScoped<
            IRequestHandler<ListInstalledModsRequest, OperationResult<IReadOnlyList<InstallRecordSummary>>>,
            UninstallModHandler>();
    }
}

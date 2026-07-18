using UnityAssetsPatcher.Application.Backups;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Manifests;
using UnityAssetsPatcher.Application.Assets;
using Microsoft.Extensions.DependencyInjection;

namespace UnityAssetsPatcher.Application.Workflows;

public sealed class WorkflowService : IWorkflowService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public WorkflowService(IServiceScopeFactory scopeFactory)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        _scopeFactory = scopeFactory;
    }

    public BackupRecoveryPreview PreviewPendingTransaction(string gameDirectory)
    {
        return Invoke<BackupRepository, BackupRecoveryPreview>(repository =>
            repository.PreviewPendingTransaction(gameDirectory));
    }

    public BackupRecoveryReport RecoverPendingTransactions(string gameDirectory)
    {
        return Invoke<BackupRepository, BackupRecoveryReport>(repository =>
            repository.RecoverPendingTransactions(gameDirectory));
    }

    public BackupRecoveryReport CheckPendingTransactions()
    {
        return Invoke<BackupRepository, BackupRecoveryReport>(repository => repository.CheckPendingTransactions());
    }

    public ModManifest CheckManifest(string path)
    {
        return Invoke<ModManifestReader, ModManifest>(reader => reader.Load(path));
    }

    public InspectListResult InspectList(InspectListRequest request)
    {
        return Invoke<InspectAssetsWorkflow, InspectListResult>(workflow => workflow.List(request));
    }

    public AssetsFieldInfo InspectFields(InspectFieldsRequest request)
    {
        return Invoke<InspectAssetsWorkflow, AssetsFieldInfo>(workflow => workflow.Fields(request));
    }

    public InstallPreviewResult PreviewInstall(InstallRequest request)
    {
        return Invoke<InstallModWorkflow, InstallPreviewResult>(workflow => workflow.Preview(request));
    }

    public InstallModResult Install(InstallRequest request)
    {
        return Invoke<InstallModWorkflow, InstallModResult>(workflow => workflow.Install(request));
    }

    public IReadOnlyList<InstallRecordSummary> ListInstalledMods()
    {
        return Invoke<UninstallModWorkflow, IReadOnlyList<InstallRecordSummary>>(workflow => workflow.ListInstalled());
    }

    public UninstallPreviewResult PreviewUninstall(UninstallPreviewRequest request)
    {
        return Invoke<UninstallModWorkflow, UninstallPreviewResult>(workflow => workflow.Preview(request));
    }

    public UninstallModResult Uninstall(UninstallModRequest request)
    {
        return Invoke<UninstallModWorkflow, UninstallModResult>(workflow => workflow.Uninstall(request));
    }

    private TResult Invoke<TService, TResult>(Func<TService, TResult> operation)
        where TService : notnull
    {
        using IServiceScope scope = _scopeFactory.CreateScope();
        return operation(scope.ServiceProvider.GetRequiredService<TService>());
    }
}

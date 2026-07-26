using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using UnityAssetsPatcher.Application.Backups;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Manifests;
using UnityAssetsPatcher.Domain.Assets;

namespace UnityAssetsPatcher.Application.Workflows;

public sealed class WorkflowService : IWorkflowService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WorkflowService> _logger;

    public WorkflowService(IServiceScopeFactory scopeFactory, ILogger<WorkflowService>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        _scopeFactory = scopeFactory;
        _logger = logger ?? NullLogger<WorkflowService>.Instance;
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

    public AssetField InspectFields(InspectFieldsRequest request)
    {
        return Invoke<InspectAssetsWorkflow, AssetField>(workflow => workflow.Fields(request));
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

    private TResult Invoke<TService, TResult>(
        Func<TService, TResult> operation,
        [CallerMemberName] string operationName = "")
        where TService : notnull
    {
        using IServiceScope scope = _scopeFactory.CreateScope();

        try
        {
            return operation(scope.ServiceProvider.GetRequiredService<TService>());
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Workflow operation {OperationName} failed", operationName);

            throw;
        }
    }
}

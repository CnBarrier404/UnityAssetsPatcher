namespace UnityAssetsPatcher.Application.Contracts;

public interface IWorkflowService
{
    public InstallPreviewResult PreviewInstall(InstallPreviewRequest request);
    public InstallModResult Install(InstallModRequest request);
    public IReadOnlyList<InstallRecordSummary> ListInstalledMods();
    public UninstallPreviewResult PreviewUninstall(UninstallPreviewRequest request);
    public UninstallModResult Uninstall(UninstallModRequest request);
}

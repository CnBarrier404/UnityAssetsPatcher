namespace UnityAssetsPatcher.Application.Contracts;

public interface IWorkflowService
{
    public InstallPreviewResult PreviewInstall(InstallRequest request);
    public InstallModResult Install(InstallRequest request);
    public IReadOnlyList<InstallRecordSummary> ListInstalledMods();
    public UninstallPreviewResult PreviewUninstall(UninstallPreviewRequest request);
    public UninstallModResult Uninstall(UninstallModRequest request);
}

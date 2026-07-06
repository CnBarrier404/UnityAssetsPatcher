using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Modules.Installation;

namespace UnityAssetsPatcher.Application.Workflows;

public sealed class InstallModWorkflow
{
    private readonly InstallPlanBuilder _planBuilder;
    private readonly InstallPlanExecutor _executor;
    private readonly InstallAssetsReadResources _assetsReadResources;
    private readonly InstallPayloadPreviewer _payloadPreviewer;
    private readonly InstallResultMapper _resultMapper;

    public InstallModWorkflow(
        InstallPlanBuilder planBuilder,
        InstallPlanExecutor executor,
        InstallAssetsReadResources assetsReadResources,
        InstallPayloadPreviewer payloadPreviewer,
        InstallResultMapper resultMapper)
    {
        _planBuilder = planBuilder;
        _executor = executor;
        _assetsReadResources = assetsReadResources;
        _payloadPreviewer = payloadPreviewer;
        _resultMapper = resultMapper;
    }

    public InstallPreviewResult Preview(InstallPreviewRequest request)
    {
        var timings = new StepTimer();

        try
        {
            using InstallPlanSession session = _planBuilder.BuildPreview(request, timings);
            InstallPatchPreview patchPreview = session.Plan.PatchPreview
                                               ?? throw new InvalidOperationException(
                                                   "Preview plan does not contain a patch preview.");
            var payloadPreview = _payloadPreviewer.Preview(session.Plan.PayloadFiles);

            return _resultMapper.ToPreviewResult(
                session.Package,
                patchPreview,
                payloadPreview,
                timings.BuildSnapshot());
        }
        finally
        {
            _assetsReadResources.Release();
        }
    }

    public InstallModResult Install(InstallModRequest request)
    {
        var timings = new StepTimer();

        try
        {
            using InstallPlanSession session = _planBuilder.BuildInstall(request, timings);
            InstallExecutionResult execution = _executor.Execute(
                session,
                request.BackupDirectory,
                timings);

            return _resultMapper.ToInstallResult(
                session.Package,
                execution.PatchApplyResult,
                execution.CopiedFiles,
                timings.BuildSnapshot());
        }
        finally
        {
            _assetsReadResources.Release();
        }
    }
}

using UnityAssetsPatcher.Application.Contracts;

namespace UnityAssetsPatcher.Application.Modules.Installation;

public sealed class InstallPlanBuilder
{
    private readonly InstallPackageSource _packageSource;
    private readonly TargetAssetResolver _targetAssetResolver;
    private readonly GameDirectoryResolver _gameDirectoryResolver;
    private readonly InstallPayloadPlanner _payloadPlanner;
    private readonly InstallPatchPlanner _patchPlanner;

    public InstallPlanBuilder(
        InstallPackageSource packageSource,
        TargetAssetResolver targetAssetResolver,
        GameDirectoryResolver gameDirectoryResolver,
        InstallPayloadPlanner payloadPlanner,
        InstallPatchPlanner patchPlanner)
    {
        _packageSource = packageSource;
        _targetAssetResolver = targetAssetResolver;
        _gameDirectoryResolver = gameDirectoryResolver;
        _payloadPlanner = payloadPlanner;
        _patchPlanner = patchPlanner;
    }

    public InstallPlanSession BuildPreview(InstallPreviewRequest request, StepTimer timings)
    {
        ModPackage? package = null;

        try
        {
            package = _packageSource.Open(request, timings);
            string gameDirectory =
                _gameDirectoryResolver.ResolveRequired(request.GameDirectory, package.Manifest.Info.Game);
            TargetAssetSet targets = _targetAssetResolver.Execute(gameDirectory, package.Manifest, timings);
            var payloadFiles = _payloadPlanner.Plan(package.Manifest, targets);
            InstallPatchPreview patchPreview = _patchPlanner.CreatePreview(targets, package, timings);
            var plan = new InstallPlan(gameDirectory, targets, payloadFiles, patchPreview, null);
            var session = new InstallPlanSession(package, plan);
            package = null;
            return session;
        }
        finally
        {
            package?.Dispose();
        }
    }

    public InstallPlanSession BuildInstall(InstallModRequest request, StepTimer timings)
    {
        ModPackage? package = null;

        try
        {
            package = _packageSource.Open(request, timings);
            string gameDirectory =
                _gameDirectoryResolver.ResolveRequired(request.GameDirectory, package.Manifest.Info.Game);
            TargetAssetSet targets = _targetAssetResolver.Execute(gameDirectory, package.Manifest, timings);
            var payloadFiles = _payloadPlanner.Plan(package.Manifest, targets);
            InstallPatchPlan patchWritePlan =
                _patchPlanner.CreateRequiredWritePlan(targets, package, timings);
            var plan = new InstallPlan(gameDirectory, targets, payloadFiles, null, patchWritePlan);
            var session = new InstallPlanSession(package, plan);
            package = null;
            return session;
        }
        finally
        {
            package?.Dispose();
        }
    }
}

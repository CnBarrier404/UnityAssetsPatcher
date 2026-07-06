namespace UnityAssetsPatcher.Application.Modules.Installation;

public sealed class InstallPlanSession : IDisposable
{
    public ModPackage Package { get; }
    public InstallPlan Plan { get; }

    public InstallPlanSession(ModPackage package, InstallPlan plan)
    {
        Package = package;
        Plan = plan;
    }

    public void Dispose()
    {
        Package.Dispose();
    }
}

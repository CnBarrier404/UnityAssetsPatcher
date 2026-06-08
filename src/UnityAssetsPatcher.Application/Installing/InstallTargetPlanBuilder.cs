using UnityAssetsPatcher.Application.Contracts;

namespace UnityAssetsPatcher.Application.Installing;

public sealed class InstallTargetPlanBuilder
{
    public IReadOnlyList<InstallPatchTarget> CreateTargets(
        string gameDirectory,
        ModManifest manifest,
        InstallTimingBuilder timings)
    {
        var targetPaths = timings.MeasureFindGameFiles(() => InstallTargetResolver.Resolve(
            gameDirectory,
            manifest.Patches.Select(patch => patch.AssetsFileName)));

        return manifest.Patches
            .GroupBy(patch => patch.AssetsFileName, StringComparer.OrdinalIgnoreCase)
            .Select(targetGroup => new InstallPatchTarget(
                targetGroup.Key,
                targetPaths[targetGroup.Key],
                targetGroup.ToArray()))
            .ToArray();
    }
}

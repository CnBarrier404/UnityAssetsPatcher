using UnityAssetsPatcher.Application.Manifests;
using UnityAssetsPatcher.Application.Contracts;

namespace UnityAssetsPatcher.Application.Modules.Installation;

public sealed class InstallPackageSource
{
    private readonly ModManifestReader _manifestReader;

    public InstallPackageSource(ModManifestReader manifestReader)
    {
        _manifestReader = manifestReader;
    }

    public ModPackage Open(InstallPreviewRequest request, StepTimer timings)
    {
        return Open(request.ZipFilePath, request.SelectedOptionalGroups, timings);
    }

    public ModPackage Open(InstallModRequest request, StepTimer timings)
    {
        return Open(request.ZipFilePath, request.SelectedOptionalGroups, timings);
    }

    private ModPackage Open(
        string zipFilePath,
        IReadOnlyList<string> selectedOptionalGroups,
        StepTimer timings)
    {
        return ModPackage.Open(zipFilePath, selectedOptionalGroups, _manifestReader, timings);
    }
}

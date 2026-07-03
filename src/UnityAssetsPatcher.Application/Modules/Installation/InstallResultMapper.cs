using UnityAssetsPatcher.Application.Contracts;

namespace UnityAssetsPatcher.Application.Modules.Installation;

public sealed class InstallResultMapper
{
    public InstallPreviewResult ToPreviewResult(
        ModPackage package,
        InstallPatchPreview patchPreview,
        IReadOnlyList<InstallChange> payloadPreview,
        TimingSnapshot timing)
    {
        var changes = patchPreview.Files
            .Select(file => new InstallChange(
                InstallChangeKind.Patch,
                file.Target,
                file.AssetsFilePath,
                Preview: file.Preview))
            .Concat(payloadPreview)
            .ToArray();

        return new InstallPreviewResult(
            package.Manifest.Info.Name,
            package.Manifest.Info.Version,
            package.Manifest.Info.Author,
            changes,
            package.OptionalGroups
                .Select(group => (group.Info.Name, group.Info.Description))
                .ToArray(),
            timing);
    }

    public InstallModResult ToInstallResult(
        ModPackage package,
        InstallPatchApplyResult patchApplyResult,
        IReadOnlyList<InstallChange> copiedFiles,
        TimingSnapshot timing)
    {
        var changes = patchApplyResult.Files
            .Select(file => new InstallChange(
                InstallChangeKind.Patch,
                file.Target,
                file.AssetsFilePath,
                BackupPath: file.BackupPath,
                AssetCount: file.AssetCount,
                OperationCount: file.OperationCount))
            .Concat(copiedFiles)
            .ToArray();

        return new InstallModResult(
            package.Manifest.Info.Name,
            package.Manifest.Info.Version,
            package.Manifest.Info.Author,
            changes,
            package.AppliedOptionalGroups,
            timing);
    }
}

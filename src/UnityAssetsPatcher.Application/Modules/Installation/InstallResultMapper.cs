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
        InstallChange[] changes = patchPreview.Files
            .Select(file => new InstallChange(
                InstallChangeKind.Patch,
                file.Target,
                file.AssetsFilePath,
                Preview: file.Preview))
            .Concat(payloadPreview)
            .ToArray();

        return new InstallPreviewResult(
            package.Manifest.Name,
            package.Manifest.Version,
            package.Manifest.Author,
            changes,
            package.OptionalGroups
                .Select(group => (group.Name, group.Description))
                .ToArray(),
            timing);
    }

    public InstallModResult ToInstallResult(
        ModPackage package,
        InstallPatchApplyResult patchApplyResult,
        IReadOnlyList<InstallChange> copiedFiles,
        TimingSnapshot timing)
    {
        InstallChange[] changes = patchApplyResult.Files
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
            package.Manifest.Name,
            package.Manifest.Version,
            package.Manifest.Author,
            changes,
            package.AppliedOptionalGroups,
            timing);
    }
}

using UnityAssetsPatcher.Application.Contracts;

namespace UnityAssetsPatcher.Application.Installation;

internal static class InstallResultMapper
{
    public static InstallPreviewResult ToPreviewResult(
        ModPackage package,
        IReadOnlyList<InstallPatchPreviewFile> patchFiles,
        IReadOnlyList<InstallChange> payloadPreview,
        TimingSnapshot timing)
    {
        var changes = patchFiles
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

    public static InstallModResult ToInstallResult(
        ModPackage package,
        IReadOnlyList<InstallPatchAppliedFile> patchedFiles,
        IReadOnlyList<InstallChange> copiedFiles,
        string installId,
        TimingSnapshot timing)
    {
        var changes = patchedFiles
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
            installId,
            package.Manifest.Name,
            package.Manifest.Version,
            changes,
            package.AppliedOptionalGroups,
            timing);
    }
}

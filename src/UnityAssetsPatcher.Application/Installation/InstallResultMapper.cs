using UnityAssetsPatcher.Application.Contracts;

namespace UnityAssetsPatcher.Application.Installation;

internal static class InstallResultMapper
{
    public static InstallPreviewResult ToPreviewResult(
        InstallAnalysis analysis,
        TimingSnapshot timing)
    {
        var changes = analysis.Targets
            .Select(file => new InstallChange(
                InstallChangeKind.Patch,
                file.Target,
                file.AssetsFilePath,
                Preview: file.PlanningResult.Preview))
            .Concat(analysis.PayloadFiles.Select(file => new InstallChange(
                InstallChangeKind.Payload,
                file.Source,
                file.DestinationPath)))
            .ToArray();

        return new InstallPreviewResult(
            analysis.Manifest.Name,
            analysis.Manifest.Version,
            analysis.Manifest.Author,
            changes,
            analysis.OptionalGroups
                .Select(group => (group.Name, group.Description))
                .ToArray(),
            timing);
    }

    public static InstallModResult ToInstallResult(
        InstallAnalysis analysis,
        IReadOnlyList<InstallPatchAppliedFile> patchedFiles,
        IReadOnlyList<InstallChange> copiedFiles,
        string installId,
        int baseSnapshotCount,
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
                analysis.Manifest.Name,
                analysis.Manifest.Version,
                changes,
                analysis.AppliedOptionalGroups,
                timing) with
            {
                BaseSnapshotCount = baseSnapshotCount,
            };
    }
}

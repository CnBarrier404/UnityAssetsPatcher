using UnityAssetsPatcher.Application.Contracts;

namespace UnityAssetsPatcher.Application.Modules.Installation;

public sealed class InstallRecordBuilder
{
    public InstallRecord Build(
        ModPackage package,
        string gameDirectory,
        InstallPatchApplyResult patchApplyResult,
        IReadOnlyList<InstallChange> copiedFiles,
        IReadOnlyList<string> appliedOptionalGroups)
    {
        return new InstallRecord(
            Guid.NewGuid().ToString("N"),
            DateTimeOffset.Now,
            package.Manifest.Name,
            package.Manifest.Version,
            package.Manifest.Author,
            package.PackagePath,
            gameDirectory,
            patchApplyResult.Files
                .Select(file => new InstallRecordPatchedFile(
                    file.Target,
                    file.AssetsFilePath,
                    file.BackupPath,
                    file.AssetCount,
                    file.OperationCount))
                .ToArray(),
            copiedFiles
                .Where(file => file.Kind == InstallChangeKind.Payload)
                .Select(file => new InstallRecordCopiedFile(
                    file.Name,
                    file.Path,
                    File.Exists(file.Path)))
                .ToArray())
        {
            OptionalGroups = appliedOptionalGroups.Count == 0 ? null : appliedOptionalGroups,
        };
    }
}

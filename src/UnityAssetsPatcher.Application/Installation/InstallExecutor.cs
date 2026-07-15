using UnityAssetsPatcher.Application.Backups;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Patching;
using UnityAssetsPatcher.Core.Assets;

namespace UnityAssetsPatcher.Application.Installation;

internal sealed record InstallExecutionResult(
    IReadOnlyList<InstallPatchAppliedFile> PatchedFiles,
    IReadOnlyList<InstallChange> CopiedFiles,
    string InstallId);

internal sealed record InstallPatchAppliedFile(
    string Target,
    string AssetsFilePath,
    string BackupPath,
    int AssetCount,
    int OperationCount);

public sealed class InstallExecutor
{
    private readonly PatchOutputWriter _patchOutputWriter;
    private readonly IAssetsAccessScope _assets;
    private readonly ModBackupStore _backupStore;

    public InstallExecutor(PatchOutputWriter patchOutputWriter, IAssetsAccessScope assets, ModBackupStore backupStore)
    {
        _patchOutputWriter = patchOutputWriter;
        _assets = assets;
        _backupStore = backupStore;
    }

    public void CloseReadSessions()
    {
        _assets.CloseReadSessions();
    }

    internal InstallExecutionResult Execute(
        InstallPlanSession<InstallWritePlan> session,
        StepTimer timings)
    {
        InstallWritePlan writePlan = session.Plan;
        InstallRecordPaths recordPaths = CreateRecordPaths(_backupStore, session.Package);

        OperationJournal journal = CreateJournal(writePlan, recordPaths);
        OperationJournalStore.Save(recordPaths.InstallDirectory, journal);

        IReadOnlyList<InstallPatchAppliedFile>? patchedFiles = null;
        IReadOnlyList<InstallChange> copiedFiles = [];

        try
        {
            patchedFiles = ApplyPatches(writePlan.PatchFiles, recordPaths, timings);
            journal = journal with { Phase = OperationPhase.AssetsChanged };
            OperationJournalStore.Save(recordPaths.InstallDirectory, journal);
            copiedFiles = CopyPayloadFiles(session.Package, writePlan.PayloadFiles, timings);
            journal = journal with { Phase = OperationPhase.PayloadChanged };
            OperationJournalStore.Save(recordPaths.InstallDirectory, journal);
            InstallRecord record = BuildRecord(
                session.Package,
                writePlan.GameDirectory,
                recordPaths.InstallDirectory,
                patchedFiles,
                copiedFiles,
                session.Package.AppliedOptionalGroups);

            _backupStore.Save(record, recordPaths.InstallDirectory);
            journal = journal with { Phase = OperationPhase.Committed };
            OperationJournalStore.Save(recordPaths.InstallDirectory, journal);
            OperationJournalStore.Delete(recordPaths.InstallDirectory);

            return new InstallExecutionResult(patchedFiles, copiedFiles, record.Id);
        }
        catch (Exception ex)
        {
            try
            {
                RollbackInstall(recordPaths, patchedFiles, copiedFiles);
            }
            catch (Exception rollbackException)
            {
                throw new InvalidOperationException(
                    "Install failed and rollback also failed.", new AggregateException(ex, rollbackException));
            }

            throw;
        }
    }

    private IReadOnlyList<InstallPatchAppliedFile> ApplyPatches(
        IReadOnlyList<InstallPatchPlanFile> patchFiles,
        InstallRecordPaths recordPaths,
        StepTimer timings)
    {
        CloseReadSessions();

        var appliedFiles = new List<InstallPatchAppliedFile>();

        try
        {
            var files = timings.Measure("apply-patches", () =>
            {
                appliedFiles.AddRange(from file in patchFiles
                    let result =
                        _patchOutputWriter.Write(file.AssetsFilePath, null, recordPaths.AssetsBackupDirectory,
                            file.PatchPlan)
                    where result.OperationCount != 0
                    let backupPath =
                        result.BackupPath ?? throw new InvalidOperationException("Patch write did not create a backup.")
                    select new InstallPatchAppliedFile(file.Target, result.OutputPath, backupPath, result.AssetCount,
                        result.OperationCount));

                return appliedFiles.ToArray();
            });

            return files;
        }
        catch (Exception ex) when (appliedFiles.Count > 0)
        {
            try
            {
                RollbackPatches(appliedFiles);
            }
            catch (Exception rollbackException)
            {
                throw new InvalidOperationException(
                    "Patch application failed and rollback also failed.",
                    new AggregateException(ex, rollbackException));
            }

            throw;
        }
    }

    private static IReadOnlyList<InstallChange> CopyPayloadFiles(
        ModPackage package,
        IReadOnlyList<InstallPayloadFilePlan> files,
        StepTimer timings)
    {
        return timings.Measure("copy-files", () =>
        {
            if (files.Count == 0)
            {
                return [];
            }

            var results = new List<InstallChange>();

            try
            {
                foreach (InstallPayloadFilePlan file in files)
                {
                    try
                    {
                        package.CopyPayloadFile(file.Source, file.DestinationPath);
                    }
                    catch (IOException ex) when (File.Exists(file.DestinationPath))
                    {
                        throw new IOException(
                            $"Payload file was created by another process during installation: {file.DestinationPath}",
                            ex);
                    }

                    results.Add(new InstallChange(InstallChangeKind.Payload, file.Source, file.DestinationPath));
                }

                return results.ToArray();
            }
            catch (Exception ex) when (results.Count > 0)
            {
                try
                {
                    RollbackPayloadFiles(results);
                }
                catch (Exception rollbackException)
                {
                    throw new InvalidOperationException(
                        "Payload copy failed and rollback also failed.",
                        new AggregateException(ex, rollbackException));
                }

                throw;
            }
        });
    }

    private InstallRecord BuildRecord(
        ModPackage package,
        string gameDirectory,
        string installDirectory,
        IReadOnlyList<InstallPatchAppliedFile> patchedFiles,
        IReadOnlyList<InstallChange> copiedFiles,
        IReadOnlyList<string> appliedOptionalGroups)
    {
        string fingerprint = GameInstanceIdentity.CreateFingerprint(gameDirectory);
        long sequence = InstallSequenceAllocator.Allocate(
            _backupStore.ListRecords().Select(entry => entry.Record), fingerprint);
        return new InstallRecord(
            InstallRecordValidator.CurrentFormatVersion,
            fingerprint,
            sequence,
            Guid.NewGuid().ToString("N"),
            DateTimeOffset.Now,
            package.Manifest.Name,
            package.Manifest.Version,
            package.Manifest.Author,
            package.Manifest.Game,
            patchedFiles
                .Select(file => new InstallRecordPatchedFile(
                    file.Target,
                    Path.GetRelativePath(gameDirectory, file.AssetsFilePath),
                    Path.GetRelativePath(installDirectory, file.BackupPath),
                    file.AssetCount,
                    file.OperationCount,
                    FileIntegrity.Create(file.AssetsFilePath),
                    FileIntegrity.Create(file.BackupPath)))
                .ToArray(),
            copiedFiles
                .Where(file => file.Kind == InstallChangeKind.Payload)
                .Select(file => new InstallRecordCopiedFile(
                    file.Name,
                    Path.GetRelativePath(gameDirectory, file.Path),
                    FileIntegrity.Create(file.Path)))
                .ToArray())
        {
            OptionalGroups = appliedOptionalGroups.Count == 0 ? null : appliedOptionalGroups,
        };
    }

    private static void RollbackInstall(
        InstallRecordPaths recordPaths,
        IReadOnlyList<InstallPatchAppliedFile>? patchedFiles,
        IReadOnlyList<InstallChange> copiedFiles)
    {
        RollbackPayloadFiles(copiedFiles);

        if (patchedFiles is not null)
        {
            RollbackPatches(patchedFiles);
        }

        if (Directory.Exists(recordPaths.InstallDirectory))
        {
            Directory.Delete(recordPaths.InstallDirectory, true);
        }
    }

    private static void RollbackPatches(IReadOnlyList<InstallPatchAppliedFile> files)
    {
        foreach (InstallPatchAppliedFile file in files.Reverse())
        {
            if (!File.Exists(file.BackupPath))
            {
                throw new FileNotFoundException(
                    $"Install rollback backup not found: {file.BackupPath}",
                    file.BackupPath);
            }

            ModBackupStore.RestoreFile(file.BackupPath, file.AssetsFilePath);
        }
    }

    private static void RollbackPayloadFiles(IReadOnlyList<InstallChange> copiedFiles)
    {
        foreach (InstallChange file in copiedFiles.Reverse())
        {
            if (file.Kind != InstallChangeKind.Payload)
            {
                continue;
            }

            if (File.Exists(file.Path))
            {
                File.Delete(file.Path);
            }
        }
    }

    private static InstallRecordPaths CreateRecordPaths(ModBackupStore backupStore, ModPackage package)
    {
        string installDirectory = backupStore.CreateInstallDirectory(
            package.Manifest.Name,
            package.Manifest.Version);

        return new InstallRecordPaths(installDirectory, Path.Combine(installDirectory, "assets"));
    }

    private static OperationJournal CreateJournal(
        InstallWritePlan plan,
        InstallRecordPaths paths)
    {
        return new OperationJournal(
            OperationJournalStore.CurrentFormatVersion,
            OperationKind.Install,
            OperationPhase.Pending,
            Path.GetFullPath(plan.GameDirectory),
            plan.PatchFiles.Select(file => new JournalPatchedFile(
                Path.GetFullPath(file.AssetsFilePath),
                Path.Combine(paths.AssetsBackupDirectory, Path.GetFileName(file.AssetsFilePath)))).ToArray(),
            plan.PayloadFiles.Select(file => new JournalPayloadFile(Path.GetFullPath(file.DestinationPath))).ToArray());
    }

    private sealed record InstallRecordPaths(string InstallDirectory, string AssetsBackupDirectory);
}

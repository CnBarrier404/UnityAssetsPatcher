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
    private readonly BackupRepository _backupRepository;

    public InstallExecutor(
        PatchOutputWriter patchOutputWriter,
        IAssetsAccessScope assets,
        BackupRepository backupRepository)
    {
        _patchOutputWriter = patchOutputWriter;
        _assets = assets;
        _backupRepository = backupRepository;
    }

    public void CloseReadSessions() => _assets.CloseReadSessions();

    internal InstallExecutionResult Execute(InstallPlanSession<InstallWritePlan> session, StepTimer timings)
    {
        InstallWritePlan plan = session.Plan;
        BackupRepositoryMetadata repository = _backupRepository.LoadMetadata();
        string installId = Guid.NewGuid().ToString("N");
        string gameDirectory = Path.GetFullPath(plan.GameDirectory);
        string fingerprint = GameInstanceIdentity.CreateFingerprint(gameDirectory);
        long sequence = InstallSequenceAllocator.Allocate(
            _backupRepository.ListRecords().Select(entry => entry.Record), fingerprint, repository.RepositoryId);
        string temporaryDirectory = _backupRepository.CreateTransactionDirectory();
        string rollbackDirectory = Path.Combine(temporaryDirectory, "rollback");
        string preparedDirectory = Path.Combine(temporaryDirectory, "prepared");
        string preparedInstallDirectory = Path.Combine(temporaryDirectory, "prepared-install");
        string backupsDirectory = Path.Combine(preparedInstallDirectory, "backups");
        var transactionFiles = new List<BackupTransactionFile>();
        var patched = new List<InstallPatchAppliedFile>();
        var copied = new List<InstallChange>();
        bool transactionSaved = false;
        BackupTransaction? transaction = null;

        try
        {
            CloseReadSessions();
            Directory.CreateDirectory(rollbackDirectory);
            Directory.CreateDirectory(preparedDirectory);
            Directory.CreateDirectory(backupsDirectory);

            for (int index = 0; index < plan.PatchFiles.Count; index++)
            {
                InstallPatchPlanFile file = plan.PatchFiles[index];
                string rollbackPath = Path.Combine(rollbackDirectory, $"assets-{index}.bin");
                string preparedPath = Path.Combine(preparedDirectory, $"assets-{index}.bin");
                string finalBackupPath = Path.Combine(backupsDirectory, $"assets-{index}.bin");
                FileIntegrity before = FileIntegrity.Create(file.AssetsFilePath);
                File.Copy(file.AssetsFilePath, rollbackPath, false);
                if (!before.Matches(rollbackPath))
                    throw new IOException($"Backup verification failed: {file.AssetsFilePath}");

                PatchApplyResult result = timings.Measure("prepare-patch", () => _patchOutputWriter.Write(
                    file.AssetsFilePath, preparedPath, file.PatchPlan));
                if (result.OperationCount == 0)
                {
                    File.Delete(rollbackPath);
                    continue;
                }

                FileIntegrity after = FileIntegrity.Create(preparedPath);
                File.Copy(rollbackPath, finalBackupPath, false);
                transactionFiles.Add(new BackupTransactionFile(
                    BackupFileKind.Assets,
                    Path.GetRelativePath(gameDirectory, file.AssetsFilePath),
                    before,
                    after,
                    Path.GetRelativePath(temporaryDirectory, rollbackPath),
                    Path.GetRelativePath(temporaryDirectory, preparedPath)));
                patched.Add(new InstallPatchAppliedFile(file.Target, file.AssetsFilePath,
                    Path.Combine(_backupRepository.GetInstallDirectory(installId), "backups",
                        Path.GetFileName(finalBackupPath)),
                    result.AssetCount, result.OperationCount));
            }

            for (int index = 0; index < plan.PayloadFiles.Count; index++)
            {
                InstallPayloadFilePlan file = plan.PayloadFiles[index];
                string preparedPath = Path.Combine(preparedDirectory, $"payload-{index}.bin");
                session.Package.CopyPayloadFile(file.Source, preparedPath);
                FileIntegrity after = FileIntegrity.Create(preparedPath);
                transactionFiles.Add(new BackupTransactionFile(BackupFileKind.Payload,
                    Path.GetRelativePath(gameDirectory, file.DestinationPath), null, after, null,
                    Path.GetRelativePath(temporaryDirectory, preparedPath)));
                copied.Add(new InstallChange(InstallChangeKind.Payload, file.Source, file.DestinationPath));
            }

            var record = new InstallRecord(
                repository.RepositoryId,
                fingerprint,
                sequence,
                installId,
                DateTimeOffset.Now,
                session.Package.Manifest.Name,
                session.Package.Manifest.Version,
                session.Package.Manifest.Author,
                session.Package.Manifest.Game,
                patched.Select((file, index) => new InstallRecordPatchedFile(
                    file.Target,
                    Path.GetRelativePath(gameDirectory, file.AssetsFilePath),
                    Path.Combine("backups", Path.GetFileName(file.BackupPath)),
                    file.AssetCount,
                    file.OperationCount,
                    transactionFiles.Where(item => item.Kind == BackupFileKind.Assets).ElementAt(index).After!,
                    FileIntegrity.Create(Path.Combine(backupsDirectory, Path.GetFileName(file.BackupPath))))).ToArray(),
                copied.Select((file, index) => new InstallRecordCopiedFile(file.Name,
                        Path.GetRelativePath(gameDirectory, file.Path),
                        transactionFiles.Where(item => item.Kind == BackupFileKind.Payload).ElementAt(index)
                            .After!))
                    .ToArray())
            {
                OptionalGroups = session.Package.AppliedOptionalGroups.Count == 0
                    ? null
                    : session.Package.AppliedOptionalGroups,
            };
            _backupRepository.WriteRecord(record, preparedInstallDirectory);

            transaction = new BackupTransaction(repository.RepositoryId, BackupOperationKind.Install, installId,
                fingerprint, transactionFiles);
            BackupTransactionStore.Save(temporaryDirectory, transaction);
            transactionSaved = true;

            ApplyPreparedFiles(transaction, temporaryDirectory, gameDirectory);
            _backupRepository.CommitInstall(preparedInstallDirectory, installId);
            Directory.Delete(temporaryDirectory, true);
            return new InstallExecutionResult(patched, copied, installId);
        }
        catch (Exception failure)
        {
            if (!transactionSaved)
            {
                if (Directory.Exists(temporaryDirectory)) Directory.Delete(temporaryDirectory, true);
                throw;
            }

            BackupRecoveryReport recovery = _backupRepository.RecoverTrustedUnderLock(transaction!, gameDirectory);
            if (recovery.Status == BackupRepositoryStatus.Locked)
                throw new BackupRecoveryException("Install failed and automatic rollback was unsafe.", recovery,
                    failure);
            throw;
        }
    }

    private static void ApplyPreparedFiles(
        BackupTransaction transaction,
        string temporaryDirectory,
        string gameDirectory)
    {
        foreach (BackupTransactionFile file in transaction.Files)
        {
            string target = BackupFileSystem.ResolveTrustedPath(gameDirectory, file.RelativePath);
            if (file.Before is null ? File.Exists(target) : !file.Before.Matches(target))
                throw new IOException($"Install target changed before mutation: {target}");
        }

        foreach (BackupTransactionFile file in transaction.Files)
        {
            string target = BackupFileSystem.ResolveTrustedPath(gameDirectory, file.RelativePath);
            string source = Path.GetFullPath(Path.Combine(temporaryDirectory,
                file.PreparedRelativePath ?? throw new InvalidOperationException("Prepared file path is missing.")));
            if (file.Before is null)
            {
                CopyNewAtomically(source, target);
            }
            else
            {
                BackupFileSystem.RestoreAtomically(source, target);
            }

            if (file.After is null || !file.After.Matches(target))
                throw new IOException($"Installed file verification failed: {target}");
        }
    }

    private static void CopyNewAtomically(string source, string destination)
    {
        string directory = Path.GetDirectoryName(destination)!;
        string temporary = Path.Combine(directory, $".{Path.GetFileName(destination)}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.Copy(source, temporary, false);
            File.Move(temporary, destination, false);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}

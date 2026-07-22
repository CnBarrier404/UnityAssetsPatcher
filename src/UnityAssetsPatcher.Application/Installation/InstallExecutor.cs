using UnityAssetsPatcher.Application.Assets;
using UnityAssetsPatcher.Application.Backups;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Patching;
using UnityAssetsPatcher.Infrastructure.IO;

namespace UnityAssetsPatcher.Application.Installation;

public sealed record InstallExecutionResult(
    IReadOnlyList<InstallPatchAppliedFile> PatchedFiles,
    IReadOnlyList<InstallChange> CopiedFiles,
    string InstallId);

public sealed record InstallPatchAppliedFile(
    string Target,
    string AssetsFilePath,
    string BackupPath,
    int AssetCount,
    int OperationCount);

public sealed class InstallExecutor
{
    private sealed record InstallPatchPlanFile(string Target, string AssetsFilePath, PatchPlan PatchPlan);

    private readonly BackupRepository _backupRepository;
    private readonly IFileOperations _fileOperations;
    private readonly IDirectoryOperations _directoryOperations;

    public InstallExecutor(
        BackupRepository backupRepository,
        IFileOperations fileOperations,
        IDirectoryOperations directoryOperations)
    {
        ArgumentNullException.ThrowIfNull(backupRepository);
        ArgumentNullException.ThrowIfNull(fileOperations);
        ArgumentNullException.ThrowIfNull(directoryOperations);
        _backupRepository = backupRepository;
        _fileOperations = fileOperations;
        _directoryOperations = directoryOperations;
    }

    public InstallExecutionResult Execute(
        ModPackage package,
        InstallAnalysis analysis,
        IAssetsFileWriter assetsWriter,
        StepTimer timings)
    {
        var patchOutputWriter = new PatchOutputWriter(assetsWriter);
        var patchFiles = CreateRequiredPatchFiles(analysis);
        BackupRepositoryMetadata repository = _backupRepository.LoadMetadata();
        string installId = Guid.NewGuid().ToString("N");
        string gameDirectory = Path.GetFullPath(analysis.GameDirectory);
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
            _directoryOperations.Create(rollbackDirectory);
            _directoryOperations.Create(preparedDirectory);
            _directoryOperations.Create(backupsDirectory);

            for (int index = 0; index < patchFiles.Count; index++)
            {
                InstallPatchPlanFile file = patchFiles[index];
                string rollbackPath = Path.Combine(rollbackDirectory, $"assets-{index}.bin");
                string preparedPath = Path.Combine(preparedDirectory, $"assets-{index}.bin");
                string finalBackupPath = Path.Combine(backupsDirectory, $"assets-{index}.bin");
                var before = FileIntegrity.Create(file.AssetsFilePath);
                File.Copy(file.AssetsFilePath, rollbackPath, false);

                if (!before.Matches(rollbackPath))
                {
                    throw new IOException($"Backup verification failed: {file.AssetsFilePath}");
                }

                PatchApplyResult result = timings.Measure("prepare-patch", () => patchOutputWriter.Write(
                    file.AssetsFilePath, preparedPath, file.PatchPlan));

                if (result.OperationCount == 0)
                {
                    File.Delete(rollbackPath);

                    continue;
                }

                var after = FileIntegrity.Create(preparedPath);

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

            for (int index = 0; index < analysis.PayloadFiles.Count; index++)
            {
                InstallPayloadFilePlan file = analysis.PayloadFiles[index];

                string preparedPath = Path.Combine(preparedDirectory, $"payload-{index}.bin");

                package.CopyPayloadFile(file.Source, preparedPath);

                var after = FileIntegrity.Create(preparedPath);

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
                analysis.Manifest.Name,
                analysis.Manifest.Version,
                analysis.Manifest.Author,
                analysis.Manifest.Game,
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
                OptionalGroups = analysis.AppliedOptionalGroups.Count == 0
                    ? null
                    : analysis.AppliedOptionalGroups,
            };

            _backupRepository.WriteRecord(record, preparedInstallDirectory);

            transaction = new BackupTransaction(repository.RepositoryId, BackupOperationKind.Install, installId,
                fingerprint, transactionFiles);
            BackupTransactionStore.Save(_fileOperations, _directoryOperations, temporaryDirectory, transaction);
            transactionSaved = true;

            ApplyPreparedFiles(transaction, temporaryDirectory, gameDirectory);
            _backupRepository.CommitInstall(preparedInstallDirectory, installId);
            _directoryOperations.Delete(temporaryDirectory);

            return new InstallExecutionResult(patched, copied, installId);
        }
        catch (Exception failure)
        {
            HandleFailure(failure, transactionSaved, transaction, temporaryDirectory, gameDirectory);

            throw;
        }
    }

    private static IReadOnlyList<InstallPatchPlanFile> CreateRequiredPatchFiles(InstallAnalysis analysis)
    {
        return analysis.Targets
            .Select(target => new InstallPatchPlanFile(
                target.Target,
                target.AssetsFilePath,
                target.PlanningResult.Plan ?? throw new InvalidOperationException(
                    "Apply analysis did not contain a patch plan.")))
            .ToArray();
    }

    private void HandleFailure(
        Exception failure,
        bool transactionSaved,
        BackupTransaction? transaction,
        string temporaryDirectory,
        string gameDirectory)
    {
        if (!transactionSaved)
        {
            if (Directory.Exists(temporaryDirectory))
            {
                _directoryOperations.Delete(temporaryDirectory);
            }

            return;
        }

        BackupRecoveryReport recovery = _backupRepository.RecoverTrustedUnderLock(transaction!, gameDirectory);

        if (recovery.Status == BackupRepositoryStatus.Locked)
        {
            throw new
                BackupRecoveryException("Install failed and automatic rollback was unsafe.", recovery, failure);
        }
    }

    private void ApplyPreparedFiles(
        BackupTransaction transaction,
        string temporaryDirectory,
        string gameDirectory)
    {
        foreach (BackupTransactionFile file in transaction.Files)
        {
            string target = BackupFileSystem.ResolveTrustedPath(gameDirectory, file.RelativePath);

            if (!file.Before?.Matches(target) ?? File.Exists(target))
            {
                throw new IOException($"Install target changed before mutation: {target}");
            }
        }

        foreach (BackupTransactionFile file in transaction.Files)
        {
            string target = BackupFileSystem.ResolveTrustedPath(gameDirectory, file.RelativePath);
            string source = Path.GetFullPath(Path.Combine(temporaryDirectory,
                file.PreparedRelativePath ?? throw new InvalidOperationException("Prepared file path is missing.")));

            _fileOperations.Copy(source, target);

            if (file.After is null || !file.After.Matches(target))
            {
                throw new IOException($"Installed file verification failed: {target}");
            }
        }
    }
}

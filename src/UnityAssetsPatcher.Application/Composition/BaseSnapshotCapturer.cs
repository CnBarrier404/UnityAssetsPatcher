using UnityAssetsPatcher.Application.Repository;
using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Domain.Integrity;

namespace UnityAssetsPatcher.Application.Composition;

public sealed class BaseSnapshotCapturer
{
    private readonly ICompositionRepository _compositionRepository;
    private readonly IFileSystemOperations _fileSystemOperations;
    private readonly TrustedPathResolver _pathResolver;

    public BaseSnapshotCapturer(
        ICompositionRepository compositionRepository,
        IFileSystemOperations fileSystemOperations)
    {
        ArgumentNullException.ThrowIfNull(compositionRepository);
        ArgumentNullException.ThrowIfNull(fileSystemOperations);

        _compositionRepository = compositionRepository;
        _fileSystemOperations = fileSystemOperations;
        _pathResolver = new TrustedPathResolver(fileSystemOperations);
    }

    public BaseCatalog Capture(
        IRepositoryOperationLock operationLock,
        string gameDirectory,
        string relativePath,
        RepositoryFileKind fileKind)
    {
        ArgumentNullException.ThrowIfNull(operationLock);
        operationLock.EnsureHeldFor(_compositionRepository.RepositoryDirectory);

        if (!Enum.IsDefined(fileKind))
        {
            throw new ArgumentOutOfRangeException(nameof(fileKind), fileKind, "Unsupported backup file kind.");
        }

        string normalizedGameDirectory = _pathResolver.ResolveExistingDirectory(gameDirectory);
        string normalizedRelativePath = NormalizeRelativePath(relativePath);
        string fingerprint = GameInstanceIdentity.CreateFingerprint(_pathResolver, normalizedGameDirectory);
        BaseCatalog? existingCatalog = _compositionRepository.BaseSnapshots.TryReadCatalog(fingerprint);

        if (existingCatalog is not null && ContainsEntry(existingCatalog, normalizedRelativePath, fileKind))
        {
            return existingCatalog;
        }

        string sourcePath = _pathResolver.ResolveWithinDirectory(normalizedGameDirectory, normalizedRelativePath);
        BaseFileEntry? assetsEntry = null;
        PayloadBaseEntry? payloadEntry = null;

        if (fileKind == RepositoryFileKind.Assets)
        {
            EnsureRegularFile(sourcePath, "Assets source");
            FileIntegrity storedIntegrity = _compositionRepository.BaseSnapshots.StoreVerifiedCopy(
                fingerprint,
                normalizedRelativePath,
                sourcePath);

            assetsEntry = new BaseFileEntry(normalizedRelativePath, storedIntegrity);
        }
        else
        {
            if (!TryGetAttributes(sourcePath, out FileAttributes attributes))
            {
                payloadEntry = new PayloadBaseEntry(normalizedRelativePath, PayloadBaseState.Absent);
            }
            else
            {
                EnsureRegularFile(attributes, sourcePath, "Payload source");
                FileIntegrity storedIntegrity = _compositionRepository.BaseSnapshots.StoreVerifiedCopy(
                    fingerprint,
                    normalizedRelativePath,
                    sourcePath);

                payloadEntry = new PayloadBaseEntry(
                    normalizedRelativePath,
                    PayloadBaseState.Present,
                    storedIntegrity);
            }
        }

        DateTimeOffset capturedAt = existingCatalog?.CapturedAt ?? DateTimeOffset.UtcNow;
        var assetsFiles = existingCatalog?.AssetsFiles.ToList() ?? [];
        var payloadTargets = existingCatalog?.PayloadTargets.ToList() ?? [];

        if (assetsEntry is not null)
        {
            assetsFiles.Add(assetsEntry);
        }

        if (payloadEntry is not null)
        {
            payloadTargets.Add(payloadEntry);
        }

        BaseCatalog updatedCatalog = new(fingerprint, capturedAt, assetsFiles, payloadTargets);

        _compositionRepository.BaseSnapshots.WriteCatalog(updatedCatalog);

        return updatedCatalog;
    }

    public BaseFileEntry? TryGetAssetsEntry(string gameDirectory, string relativePath)
    {
        BaseCatalog? catalog = TryReadCatalog(gameDirectory, relativePath, out string normalizedRelativePath);

        return catalog?.AssetsFiles.FirstOrDefault(file =>
            TrustedPath.PathComparer.Equals(file.RelativePath, normalizedRelativePath));
    }

    public PayloadBaseEntry? TryGetPayloadEntry(string gameDirectory, string relativePath)
    {
        BaseCatalog? catalog = TryReadCatalog(gameDirectory, relativePath, out string normalizedRelativePath);

        return catalog?.PayloadTargets.FirstOrDefault(file =>
            TrustedPath.PathComparer.Equals(file.RelativePath, normalizedRelativePath));
    }

    private BaseCatalog? TryReadCatalog(
        string gameDirectory,
        string relativePath,
        out string normalizedRelativePath)
    {
        string normalizedGameDirectory = _pathResolver.ResolveExistingDirectory(gameDirectory);
        normalizedRelativePath = NormalizeRelativePath(relativePath);
        string fingerprint = GameInstanceIdentity.CreateFingerprint(_pathResolver, normalizedGameDirectory);

        return _compositionRepository.BaseSnapshots.TryReadCatalog(fingerprint);
    }

    private static bool ContainsEntry(BaseCatalog catalog, string relativePath, RepositoryFileKind fileKind)
    {
        return fileKind == RepositoryFileKind.Assets
            ? catalog.AssetsFiles.Any(file => TrustedPath.PathComparer.Equals(file.RelativePath, relativePath))
            : catalog.PayloadTargets.Any(file => TrustedPath.PathComparer.Equals(file.RelativePath, relativePath));
    }

    private void EnsureRegularFile(string path, string description)
    {
        FileAttributes attributes = _fileSystemOperations.GetAttributes(path);
        EnsureRegularFile(attributes, path, description);
    }

    private static void EnsureRegularFile(FileAttributes attributes, string path, string description)
    {
        if (attributes.HasFlag(FileAttributes.Directory) || attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            throw new IOException($"{description} must be a regular file: {path}");
        }
    }

    private bool TryGetAttributes(string path, out FileAttributes attributes)
    {
        try
        {
            attributes = _fileSystemOperations.GetAttributes(path);

            return true;
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            attributes = default;

            return false;
        }
    }

    private static string NormalizeRelativePath(string relativePath)
    {
        return !TrustedPath.TryNormalizeRelativePath(relativePath, out string normalizedPath)
            ? throw new ArgumentException($"The relative path is not trusted: '{relativePath}'.", nameof(relativePath))
            : normalizedPath;
    }
}

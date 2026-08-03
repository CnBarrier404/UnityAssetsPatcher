using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Application.Operations;
using UnityAssetsPatcher.Application.Packages;

namespace UnityAssetsPatcher.Application.Manifests;

public sealed class ManifestSourceReader
{
    private readonly ModPackageArchiveService _archiveService;
    private readonly IFileSystemOperations _fileSystemOperations;

    public ManifestSourceReader(
        ModPackageArchiveService archiveService,
        IFileSystemOperations fileSystemOperations)
    {
        ArgumentNullException.ThrowIfNull(archiveService);
        ArgumentNullException.ThrowIfNull(fileSystemOperations);

        _archiveService = archiveService;
        _fileSystemOperations = fileSystemOperations;
    }

    public async Task<OperationResult<byte[]>> ReadAsync(
        string sourcePath,
        CancellationToken cancellationToken)
    {
        if (!TryGetFullPath(sourcePath, out string fullSourcePath))
        {
            return Failure(FileErrorCodes.InvalidPath, sourcePath);
        }

        if (Path.GetExtension(fullSourcePath).Equals(".zip", StringComparison.OrdinalIgnoreCase))
        {
            return await ReadPackageAsync(fullSourcePath, cancellationToken).ConfigureAwait(false);
        }

        using Stream input = _fileSystemOperations.OpenRead(fullSourcePath);
        using MemoryStream output = new();

        await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);

        return new OperationSucceeded<byte[]>(output.ToArray());
    }

    private async Task<OperationResult<byte[]>> ReadPackageAsync(
        string packagePath,
        CancellationToken cancellationToken)
    {
        OperationResult<ModPackageArchiveSession> openResult = _archiveService.OpenRead(packagePath);

        if (openResult is OperationFailed<ModPackageArchiveSession> failure)
        {
            return new OperationFailed<byte[]>(failure.Error);
        }

        using ModPackageArchiveSession session = ((OperationSucceeded<ModPackageArchiveSession>)openResult).Value;

        return await session.ReadManifestAsync(cancellationToken).ConfigureAwait(false);
    }

    private static bool TryGetFullPath(string sourcePath, out string fullSourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            fullSourcePath = sourcePath;

            return false;
        }

        try
        {
            fullSourcePath = Path.GetFullPath(sourcePath);

            return true;
        }
        catch (ArgumentException)
        {
            fullSourcePath = sourcePath;

            return false;
        }
        catch (NotSupportedException)
        {
            fullSourcePath = sourcePath;

            return false;
        }
    }

    private static OperationFailed<byte[]> Failure(OperationErrorCode code, string? sourcePath)
    {
        return new OperationFailed<byte[]>(new OperationError(
            code,
            new Dictionary<string, object?>
            {
                ["path"] = sourcePath,
            }));
    }
}

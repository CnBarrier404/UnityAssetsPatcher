using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Application.Operations;
using UnityAssetsPatcher.Application.Packages;

namespace UnityAssetsPatcher.Application.Manifests;

internal sealed class ManifestSourceReader
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

    public async Task<byte[]> ReadAsync(
        string sourcePath,
        CancellationToken cancellationToken)
    {
        string fullSourcePath = GetFullPath(sourcePath);

        if (Path.GetExtension(fullSourcePath).Equals(".zip", StringComparison.OrdinalIgnoreCase))
        {
            return await ReadPackageAsync(fullSourcePath, cancellationToken).ConfigureAwait(false);
        }

        using Stream input = _fileSystemOperations.OpenRead(fullSourcePath);
        using MemoryStream output = new();

        await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);

        return output.ToArray();
    }

    private async Task<byte[]> ReadPackageAsync(
        string packagePath,
        CancellationToken cancellationToken)
    {
        OperationResult<ModPackageArchiveSession> openResult = _archiveService.OpenRead(packagePath);

        using ModPackageArchiveSession session = RequirePackageResult(openResult);

        OperationResult<byte[]> manifestResult = await session
            .ReadManifestAsync(cancellationToken)
            .ConfigureAwait(false);

        return RequirePackageResult(manifestResult);
    }

    private static string GetFullPath(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            throw InvalidPath(sourcePath);
        }

        try
        {
            return Path.GetFullPath(sourcePath);
        }
        catch (ArgumentException exception)
        {
            throw InvalidPath(sourcePath, exception);
        }
        catch (NotSupportedException exception)
        {
            throw InvalidPath(sourcePath, exception);
        }
    }

    private static T RequirePackageResult<T>(OperationResult<T> result)
    {
        return result switch
        {
            OperationSucceeded<T> succeeded => succeeded.Value,
            OperationFailed<T> failed => throw new PackageException(
                failed.Error.Code.Value,
                failed.Error.Parameters),
            _ => throw new InvalidOperationException(
                "The package archive operation returned an unknown result."),
        };
    }

    private static FileOperationException InvalidPath(
        string? sourcePath,
        Exception? innerException = null)
    {
        return new FileOperationException(
            FileErrorCodes.InvalidPath.Value,
            new Dictionary<string, object?>
            {
                ["path"] = sourcePath,
            },
            innerException);
    }
}

using UnityAssetsPatcher.Application.Failures;
using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Application.Operations;
using UnityAssetsPatcher.Application.Packages;

namespace UnityAssetsPatcher.Application.Manifests;

internal sealed class ModManifestService : IModManifestService
{
    private readonly ManifestSourceReader _sourceReader;

    public ModManifestService(ManifestSourceReader sourceReader)
    {
        ArgumentNullException.ThrowIfNull(sourceReader);

        _sourceReader = sourceReader;
    }

    public async Task<ModManifest> ReadManifestAsync(
        string sourcePath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            byte[] manifestBytes = await _sourceReader
                .ReadAsync(sourcePath, cancellationToken)
                .ConfigureAwait(false);

            return ModManifestParser.Parse(manifestBytes);
        }
        catch (FileNotFoundException exception)
        {
            throw FileFailure(FileErrorCodes.NotFound, sourcePath, exception);
        }
        catch (DirectoryNotFoundException exception)
        {
            throw FileFailure(FileErrorCodes.NotFound, sourcePath, exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            throw FileFailure(FileErrorCodes.AccessDenied, sourcePath, exception);
        }
        catch (InvalidDataException exception) when (IsPackagePath(sourcePath))
        {
            throw PackageFailure(ModPackageErrorCodes.InvalidArchive, sourcePath, exception);
        }
        catch (IOException exception)
        {
            throw FileFailure(FileErrorCodes.ReadFailed, sourcePath, exception);
        }
    }

    private static FileOperationException FileFailure(
        OperationErrorCode code,
        string? sourcePath,
        Exception innerException)
    {
        return new FileOperationException(
            code.Value,
            new Dictionary<string, object?>
            {
                ["path"] = sourcePath,
            },
            innerException);
    }

    private static PackageException PackageFailure(
        OperationErrorCode code,
        string? sourcePath,
        Exception innerException)
    {
        return new PackageException(
            code.Value,
            new Dictionary<string, object?>
            {
                ["package_path"] = sourcePath,
            },
            innerException);
    }

    private static bool IsPackagePath(string? sourcePath)
    {
        return sourcePath is not null &&
               Path.GetExtension(sourcePath).Equals(".zip", StringComparison.OrdinalIgnoreCase);
    }
}

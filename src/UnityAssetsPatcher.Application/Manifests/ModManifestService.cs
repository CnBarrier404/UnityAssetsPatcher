using UnityAssetsPatcher.Application.Failures;
using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Application.Operations;
using UnityAssetsPatcher.Application.Packages;

namespace UnityAssetsPatcher.Application.Manifests;

public sealed class ModManifestService : IModManifestService
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
        OperationResult<byte[]> readResult;

        try
        {
            readResult = await _sourceReader
                .ReadAsync(sourcePath, cancellationToken)
                .ConfigureAwait(false);
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

        byte[] manifestBytes = RequireSource(readResult);
        var parseResult = ModManifestParser.Parse(manifestBytes);

        return RequireManifest(parseResult);
    }

    private static byte[] RequireSource(OperationResult<byte[]> result)
    {
        return result switch
        {
            OperationSucceeded<byte[]> succeeded => succeeded.Value,
            OperationFailed<byte[]> failed => throw ToFailure(failed.Error),
            _ => throw new InvalidOperationException("The manifest source reader returned an unknown result.")
        };
    }

    private static ModManifest RequireManifest(OperationResult<ModManifest> result)
    {
        return result switch
        {
            OperationSucceeded<ModManifest> succeeded => succeeded.Value,
            OperationFailed<ModManifest> failed => throw ToFailure(failed.Error),
            _ => throw new InvalidOperationException("The manifest parser returned an unknown result.")
        };
    }

    private static ApplicationFailureException ToFailure(OperationError error)
    {
        string code = error.Code.Value;

        if (code.StartsWith("file.", StringComparison.Ordinal))
        {
            return new FileOperationException(code, error.Parameters);
        }

        if (code.StartsWith("mod_package.", StringComparison.Ordinal))
        {
            return new PackageException(code, error.Parameters);
        }

        if (code.StartsWith("manifest.", StringComparison.Ordinal))
        {
            return new ManifestException(code, error.Parameters);
        }

        throw new InvalidOperationException($"The manifest operation returned an unsupported error code: {code}.");
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

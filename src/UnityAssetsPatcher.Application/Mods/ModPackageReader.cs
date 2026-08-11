using Microsoft.Extensions.Logging;
using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Application.Operations;

namespace UnityAssetsPatcher.Application.Mods;

public sealed class ModPackageReader
{
    private readonly IModPackageReader _packageReader;
    private readonly IFileSystemOperations _fileSystemOperations;
    private readonly ILoggerFactory _loggerFactory;

    public ModPackageReader(
        IModPackageReader packageReader,
        IFileSystemOperations fileSystemOperations,
        ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(packageReader);
        ArgumentNullException.ThrowIfNull(fileSystemOperations);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        _packageReader = packageReader;
        _fileSystemOperations = fileSystemOperations;
        _loggerFactory = loggerFactory;
    }

    public async Task<OperationResult<byte[]>> ReadManifestAsync(
        string packagePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);
        cancellationToken.ThrowIfCancellationRequested();

        string fullPackagePath = Path.GetFullPath(packagePath);

        try
        {
            using IModPackageSession package = await _packageReader
                .OpenAsync(fullPackagePath, cancellationToken)
                .ConfigureAwait(false);
            OperationResult<IModPackageEntry> entryResult = ModPackageManifest.FindEntry(
                package,
                fullPackagePath,
                cancellationToken);

            if (entryResult is OperationFailed<IModPackageEntry> entryFailure)
            {
                return new OperationFailed<byte[]>(entryFailure.Error);
            }

            IModPackageEntry manifestEntry = ((OperationSucceeded<IModPackageEntry>)entryResult).Value;

            return await ModPackageManifest.ReadAsync(
                manifestEntry,
                fullPackagePath,
                _loggerFactory.CreateLogger<ModPackageReader>(),
                cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidDataException)
        {
            return InvalidArchive<byte[]>(fullPackagePath);
        }
    }

    internal async Task<OperationResult<ModPackageSession>> OpenAsync(
        string packagePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);
        cancellationToken.ThrowIfCancellationRequested();

        string fullPackagePath = Path.GetFullPath(packagePath);
        IModPackageSession? archive = null;

        try
        {
            archive = await _packageReader
                .OpenAsync(fullPackagePath, cancellationToken)
                .ConfigureAwait(false);
            OperationResult<ModPackageIndex> indexResult = ModPackageValidator.Validate(
                archive,
                fullPackagePath,
                cancellationToken);

            if (indexResult is OperationFailed<ModPackageIndex> indexFailure)
            {
                return new OperationFailed<ModPackageSession>(indexFailure.Error);
            }

            ModPackageIndex index = ((OperationSucceeded<ModPackageIndex>)indexResult).Value;
            var session = new ModPackageSession(
                fullPackagePath,
                archive,
                index,
                _fileSystemOperations,
                _loggerFactory.CreateLogger<ModPackageSession>());
            archive = null;

            return new OperationSucceeded<ModPackageSession>(session);
        }
        catch (InvalidDataException)
        {
            return InvalidArchive<ModPackageSession>(fullPackagePath);
        }
        finally
        {
            archive?.Dispose();
        }
    }

    private static OperationFailed<T> InvalidArchive<T>(string packagePath)
    {
        return new OperationFailed<T>(new OperationError(
            ModPackageErrorCodes.InvalidArchive,
            new Dictionary<string, object?>
            {
                ["package_path"] = packagePath,
            }));
    }
}

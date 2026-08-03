using System.IO.Compression;
using Microsoft.Extensions.Logging;
using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Application.Packages;

namespace UnityAssetsPatcher.Infrastructure.Packages;

public sealed class ModPackageArchiveFactory : IModPackageArchiveFactory
{
    private readonly IFileSystemOperations _fileSystemOperations;
    private readonly ILogger<ModPackageArchiveFactory> _logger;

    public ModPackageArchiveFactory(
        IFileSystemOperations fileSystemOperations,
        ILogger<ModPackageArchiveFactory> logger)
    {
        ArgumentNullException.ThrowIfNull(fileSystemOperations);
        ArgumentNullException.ThrowIfNull(logger);

        _fileSystemOperations = fileSystemOperations;
        _logger = logger;
    }

    public IModPackageArchive OpenRead(string packagePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);

        string fullPackagePath = Path.GetFullPath(packagePath);

        ModPackageLog.OpeningPackage(_logger, fullPackagePath);

        Stream? stream = null;
        ZipArchive? archive = null;

        try
        {
            stream = _fileSystemOperations.OpenRead(fullPackagePath);
            archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
            stream = null;

            var package = new ModPackageArchive(fullPackagePath, archive, _logger);

            archive = null;

            ModPackageLog.PackageOpened(_logger, fullPackagePath, package.Entries.Count);

            return package;
        }
        catch (InvalidDataException exception)
        {
            ModPackageLog.PackageOpenFailed(_logger, fullPackagePath, exception);

            throw;
        }
        finally
        {
            archive?.Dispose();

            stream?.Dispose();
        }
    }
}

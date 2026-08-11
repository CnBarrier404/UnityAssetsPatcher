using System.IO.Compression;
using Microsoft.Extensions.Logging;
using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Application.Mods;

namespace UnityAssetsPatcher.Infrastructure.Mods;

public sealed class ModPackageReader : IModPackageReader
{
    private readonly IFileSystemOperations _fileSystemOperations;
    private readonly ILoggerFactory _loggerFactory;

    public ModPackageReader(IFileSystemOperations fileSystemOperations, ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(fileSystemOperations);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        _fileSystemOperations = fileSystemOperations;
        _loggerFactory = loggerFactory;
    }

    public async Task<byte[]> ReadManifestAsync(
        string packagePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);
        cancellationToken.ThrowIfCancellationRequested();

        string fullPackagePath = Path.GetFullPath(packagePath);
        await using Stream stream = _fileSystemOperations.OpenRead(fullPackagePath);
        await using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
        ZipArchiveEntry manifestEntry = ModPackageManifest.FindEntry(
            archive,
            fullPackagePath,
            cancellationToken);

        return await ModPackageManifest.ReadAsync(
            manifestEntry,
            fullPackagePath,
            _loggerFactory.CreateLogger<ModPackageReader>(),
            cancellationToken).ConfigureAwait(false);
    }

    public IModPackageSession Open(string packagePath)
    {
        return ModPackageSession.Open(
            packagePath,
            _fileSystemOperations,
            _loggerFactory.CreateLogger<ModPackageSession>());
    }
}

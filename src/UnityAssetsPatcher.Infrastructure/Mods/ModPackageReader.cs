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

    public IModPackageSession Open(string packagePath)
    {
        return ModPackageSession.Open(
            packagePath,
            _fileSystemOperations,
            _loggerFactory.CreateLogger<ModPackageSession>());
    }
}

using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Application.Mods;

namespace UnityAssetsPatcher.Infrastructure.Mods;

public sealed class ModPackageReader : IModPackageReader
{
    private readonly IFileSystemOperations _fileSystemOperations;

    public ModPackageReader(IFileSystemOperations fileSystemOperations)
    {
        ArgumentNullException.ThrowIfNull(fileSystemOperations);

        _fileSystemOperations = fileSystemOperations;
    }

    public IModPackageSession Open(string packagePath)
    {
        return ModPackageSession.Open(packagePath, _fileSystemOperations);
    }
}

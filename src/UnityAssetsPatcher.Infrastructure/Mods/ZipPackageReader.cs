using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Application.Mods;
using UnityAssetsPatcher.Application.Operations;

namespace UnityAssetsPatcher.Infrastructure.Mods;

public sealed class ZipPackageReader : IPackageReader
{
    private readonly IFileSystemOperations _fileSystemOperations;

    public ZipPackageReader(IFileSystemOperations fileSystemOperations)
    {
        ArgumentNullException.ThrowIfNull(fileSystemOperations);

        _fileSystemOperations = fileSystemOperations;
    }

    public OperationResult<IPackageSession> Open(string packagePath)
    {
        return ZipPackageSession.Open(packagePath, _fileSystemOperations);
    }
}

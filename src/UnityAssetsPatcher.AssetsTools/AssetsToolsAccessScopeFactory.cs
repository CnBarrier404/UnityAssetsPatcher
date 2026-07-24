using UnityAssetsPatcher.Abstractions.Assets;
using UnityAssetsPatcher.Abstractions.IO;

namespace UnityAssetsPatcher.AssetsTools;

public sealed class AssetsToolsAccessScopeFactory : IAssetsAccessScopeFactory
{
    private readonly ClassPackageCache _classPackageCache;
    private readonly IFileSystemOperations _fileSystemOperations;

    public AssetsToolsAccessScopeFactory(
        Func<Stream> openTpkStream,
        IFileSystemOperations fileSystemOperations)
    {
        ArgumentNullException.ThrowIfNull(openTpkStream);
        ArgumentNullException.ThrowIfNull(fileSystemOperations);
        _classPackageCache = new ClassPackageCache(openTpkStream);
        _fileSystemOperations = fileSystemOperations;
    }

    public IAssetsAccessScope CreateScope()
    {
        return new AssetsToolsAccessScope(_classPackageCache, _fileSystemOperations);
    }
}

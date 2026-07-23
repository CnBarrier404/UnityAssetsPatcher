using UnityAssetsPatcher.Abstractions.Assets;
using UnityAssetsPatcher.Abstractions.IO;

namespace UnityAssetsPatcher.AssetsTools;

public sealed class AssetsToolsAccessScopeFactory : IAssetsAccessScopeFactory
{
    private readonly ClassPackageCache _classPackageCache;
    private readonly IFileOperations _fileOperations;
    private readonly IDirectoryOperations _directoryOperations;

    public AssetsToolsAccessScopeFactory(
        Func<Stream> openTpkStream,
        IFileOperations fileOperations,
        IDirectoryOperations directoryOperations)
    {
        ArgumentNullException.ThrowIfNull(openTpkStream);
        ArgumentNullException.ThrowIfNull(fileOperations);
        ArgumentNullException.ThrowIfNull(directoryOperations);
        _classPackageCache = new ClassPackageCache(openTpkStream);
        _fileOperations = fileOperations;
        _directoryOperations = directoryOperations;
    }

    public IAssetsAccessScope CreateScope()
    {
        return new AssetsToolsAccessScope(_classPackageCache, _fileOperations, _directoryOperations);
    }
}

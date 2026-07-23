using UnityAssetsPatcher.Abstractions.Assets;
using UnityAssetsPatcher.Abstractions.IO;

namespace UnityAssetsPatcher.AssetsTools;

public sealed class AssetsToolsAccessScopeFactory : IAssetsAccessScopeFactory
{
    private readonly Func<Stream> _openTpkStream;
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
        _openTpkStream = openTpkStream;
        _fileOperations = fileOperations;
        _directoryOperations = directoryOperations;
    }

    public IAssetsAccessScope CreateScope()
    {
        return new AssetsToolsAccessScope(_openTpkStream, _fileOperations, _directoryOperations);
    }
}

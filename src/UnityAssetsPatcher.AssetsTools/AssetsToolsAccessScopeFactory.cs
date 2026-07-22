using UnityAssetsPatcher.Application.Assets;
using UnityAssetsPatcher.Infrastructure.IO;

namespace UnityAssetsPatcher.AssetsTools;

public sealed class AssetsToolsAccessScopeFactory : IAssetsAccessScopeFactory
{
    private readonly AssetsToolsContext _context;
    private readonly IFileOperations _fileOperations;
    private readonly IDirectoryOperations _directoryOperations;

    public AssetsToolsAccessScopeFactory(
        AssetsToolsContext context,
        IFileOperations fileOperations,
        IDirectoryOperations directoryOperations)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(fileOperations);
        ArgumentNullException.ThrowIfNull(directoryOperations);
        _context = context;
        _fileOperations = fileOperations;
        _directoryOperations = directoryOperations;
    }

    public IAssetsAccessScope CreateScope()
    {
        return new AssetsToolsAccessScope(
            () => new AssetsFileReader(_context, ownsContext: false),
            () => new AssetsFileWriter(_context, _fileOperations, _directoryOperations));
    }
}

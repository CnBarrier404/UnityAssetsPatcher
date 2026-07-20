using UnityAssetsPatcher.Application.Assets;

namespace UnityAssetsPatcher.AssetsTools;

public sealed class AssetsToolsAccessScopeFactory : IAssetsAccessScopeFactory
{
    private readonly AssetsToolsContext _context;

    public AssetsToolsAccessScopeFactory(AssetsToolsContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    public IAssetsAccessScope CreateScope()
    {
        return new AssetsToolsAccessScope(
            () => new AssetsFileReader(_context, ownsContext: false),
            () => new AssetsFileWriter(_context));
    }
}

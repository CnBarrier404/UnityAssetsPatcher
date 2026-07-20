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
        var reader = new AssetsFileReader(_context, ownsContext: false);
        var writer = new AssetsFileWriter(_context);

        return new AssetsToolsAccessScope(reader, writer);
    }
}

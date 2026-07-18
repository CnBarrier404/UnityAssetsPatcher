using UnityAssetsPatcher.Application.Assets;

namespace UnityAssetsPatcher.AssetsTools;

public sealed class AssetsToolsAccessScopeFactory : IAssetsAccessScopeFactory, IDisposable
{
    private readonly AssetsToolsContext _context;

    public AssetsToolsAccessScopeFactory(string tpkFilePath)
    {
        _context = new AssetsToolsContext(tpkFilePath);
    }

    public AssetsToolsAccessScopeFactory(Func<Stream> openTpkStream)
    {
        _context = new AssetsToolsContext(openTpkStream);
    }

    public IAssetsAccessScope CreateScope()
    {
        return new AssetsToolsAccessScope(new AssetsFileReader(_context, ownsContext: false),
            new AssetsFileWriter(_context));
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}

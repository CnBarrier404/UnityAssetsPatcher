using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace UnityAssetsPatcher.AssetsTools;

internal sealed class AssetsFileSession : IDisposable
{
    private AssetsFileInstance AssetsFileInstance { get; }

    public AssetsFile AssetsFile => AssetsFileInstance.file;

    private readonly AssetsToolsContext _context;

    private AssetsFileSession(AssetsToolsContext context, AssetsFileInstance assetsFileInstance)
    {
        _context = context;
        AssetsFileInstance = assetsFileInstance;
    }

    public static AssetsFileSession Open(string assetsFilePath, AssetsToolsContext context)
    {
        if (!File.Exists(assetsFilePath))
        {
            throw new FileNotFoundException($"Assets file not found: {assetsFilePath}", assetsFilePath);
        }

        AssetsFileInstance? assetsFileInstance = null;

        try
        {
            assetsFileInstance = context.LoadAssetsFile(assetsFilePath);

            return new AssetsFileSession(context, assetsFileInstance);
        }
        catch
        {
            if (assetsFileInstance is not null)
            {
                context.UnloadAssetsFile(assetsFileInstance);
            }

            throw;
        }
    }

    public AssetTypeValueField GetBaseField(long pathId)
    {
        return _context.GetBaseField(AssetsFileInstance, pathId);
    }

    public void Dispose()
    {
        _context.UnloadAssetsFile(AssetsFileInstance);
    }
}

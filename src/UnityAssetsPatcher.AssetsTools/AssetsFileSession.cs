using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace UnityAssetsPatcher.AssetsTools;

internal sealed class AssetsFileSession : IDisposable
{
    public AssetsManager Manager { get; }
    public AssetsFileInstance AssetsFileInstance { get; }
    public AssetsFile AssetsFile => AssetsFileInstance.file;

    private AssetsFileSession(AssetsManager manager, AssetsFileInstance assetsFileInstance)
    {
        Manager = manager;
        AssetsFileInstance = assetsFileInstance;
    }

    public static AssetsFileSession Open(string assetsFilePath, string tpkFilePath)
    {
        if (!File.Exists(assetsFilePath))
        {
            throw new FileNotFoundException($"Assets file not found: {assetsFilePath}", assetsFilePath);
        }

        if (!File.Exists(tpkFilePath))
        {
            throw new FileNotFoundException($"TPK file not found: {tpkFilePath}", tpkFilePath);
        }

        var manager = new AssetsManager();

        try
        {
            manager.LoadClassPackage(tpkFilePath);
            AssetsFileInstance assetsFileInstance = manager.LoadAssetsFile(assetsFilePath, true);

            manager.LoadClassDatabaseFromPackage(assetsFileInstance.file.Metadata.UnityVersion);

            return new AssetsFileSession(manager, assetsFileInstance);
        }
        catch
        {
            manager.UnloadAll(true);

            throw;
        }
    }

    public void Dispose()
    {
        Manager.UnloadAll(true);
    }
}

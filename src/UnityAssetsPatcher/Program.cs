using UnityAssetsPatcher.AssetsTools;

namespace UnityAssetsPatcher;

public static class Program
{
    public static int Main(string[] args)
    {
        // TPK 是随程序分发的类型数据库资源，不依赖启动时的工作目录
        // 获取：https://github.com/AssetRipper/Tpk
        string tpkFilePath = Path.Combine(AppContext.BaseDirectory, "Assets", "AssetsRipper.tpk");

        var app = new ConsoleApp(new AssetsToolsReader(tpkFilePath), Console.Out, Console.Error);

        return app.Run(args);
    }
}
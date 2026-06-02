using UnityAssetsPatcher.Core;

namespace UnityAssetsPatcher;

public sealed class ConsoleApp
{
    private readonly IAssetsReader _assetsReader;
    private readonly TextWriter _output;
    private readonly TextWriter _error;

    public ConsoleApp(IAssetsReader assetsReader, TextWriter output, TextWriter error)
    {
        _assetsReader = assetsReader;
        _output = output;
        _error = error;
    }

    public int Run(string[] args)
    {
        if (args.Length != 2 || !string.Equals(args[0], "inspect", StringComparison.OrdinalIgnoreCase))
        {
            _error.WriteLine("Usage: UnityAssetsPatcher inspect <assets-file>");
            return 1;
        }

        try
        {
            var assets = _assetsReader.ReadAssetsInfo(args[1]);

            _output.WriteLine($"{"Path ID",12} | {"Type ID",7} | {"Type Name",-24} | {"Byte Size",10}");
            _output.WriteLine(new string('-', 64));

            foreach (AssetsInfo asset in assets)
            {
                _output.WriteLine($"{asset.PathId,12} | {asset.TypeId,7} | {asset.TypeName,-24} | {asset.ByteSize,10}");
            }

            return 0;
        }
        catch (Exception exception)
        {
            _error.WriteLine(exception.Message);
            return 1;
        }
    }
}
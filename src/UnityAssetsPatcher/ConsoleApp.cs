using UnityAssetsPatcher.Core;

namespace UnityAssetsPatcher;

public sealed class ConsoleApp
{
    private const int DefaultAssetSummaryLimit = 200;

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
        if (!IsInspectCommand(args))
        {
            _error.WriteLine("Usage:");
            _error.WriteLine("  UnityAssetsPatcher inspect <assets-file> [--limit <count> | --all]");
            _error.WriteLine("  UnityAssetsPatcher inspect <assets-file> <path-id> --detail");
            return 1;
        }

        try
        {
            if (IsInspectDetailCommand(args))
            {
                return PrintAssetsFieldTree(args);
            }

            var assets = _assetsReader.ReadAssetsInfo(args[1]);
            int? limit = GetAssetSummaryLimit(args);
            var assetsToPrint = limit is null ? assets : assets.Take(limit.Value);

            _output.WriteLine($"{"Path ID",12} | {"Type ID",7} | {"Type Name",-24} | {"Byte Size",10}");
            _output.WriteLine(new string('-', 64));

            foreach (AssetsInfo asset in assetsToPrint)
            {
                _output.WriteLine($"{asset.PathId,12} | {asset.TypeId,7} | {asset.TypeName,-24} | {asset.ByteSize,10}");
            }

            if (limit is null || assets.Count <= limit.Value)
            {
                return 0;
            }

            _output.WriteLine();
            _output.WriteLine(
                $"Showing {limit.Value} of {assets.Count} assets. Use --all to print every row or --limit <count> to choose a different limit.");

            return 0;
        }
        catch (Exception exception)
        {
            _error.WriteLine(exception.Message);

            return 1;
        }
    }

    private static bool IsInspectCommand(string[] args)
    {
        if (args.Length == 2 || IsInspectSummaryOption(args))
        {
            return string.Equals(args[0], "inspect", StringComparison.OrdinalIgnoreCase);
        }

        return IsInspectDetailCommand(args);
    }

    private static bool IsInspectSummaryOption(string[] args)
    {
        return args.Length == 3 && string.Equals(args[2], "--all", StringComparison.OrdinalIgnoreCase)
               || args.Length == 4
               && string.Equals(args[2], "--limit", StringComparison.OrdinalIgnoreCase)
               && int.TryParse(args[3], out int limit)
               && limit > 0;
    }

    private static int? GetAssetSummaryLimit(string[] args)
    {
        return args.Length switch
        {
            3 when string.Equals(args[2], "--all", StringComparison.OrdinalIgnoreCase) => null,
            4 when string.Equals(args[2], "--limit", StringComparison.OrdinalIgnoreCase) => int.Parse(args[3]),
            _ => DefaultAssetSummaryLimit
        };
    }

    private static bool IsInspectDetailCommand(string[] args)
    {
        return args.Length == 4
               && string.Equals(args[0], "inspect", StringComparison.OrdinalIgnoreCase)
               && long.TryParse(args[2], out _)
               && string.Equals(args[3], "--detail", StringComparison.OrdinalIgnoreCase);
    }

    private int PrintAssetsFieldTree(string[] args)
    {
        long pathId = long.Parse(args[2]);
        AssetsFieldInfo fieldTree = _assetsReader.ReadAssetsFieldInfo(args[1], pathId);

        PrintAssetsField(fieldTree, 0);
        return 0;
    }

    private void PrintAssetsField(AssetsFieldInfo field, int depth)
    {
        string indentation = new(' ', depth * 2);
        string value = field.Value is null ? string.Empty : $": {field.Value}";
        _output.WriteLine($"{indentation}{field.Name} ({field.TypeName}){value}");

        foreach (AssetsFieldInfo child in field.Children)
        {
            PrintAssetsField(child, depth + 1);
        }
    }
}

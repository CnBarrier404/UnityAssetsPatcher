using System.Globalization;
using System.Text.Json;
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
        if (!IsSupportedCommand(args))
        {
            _error.WriteLine("Usage:");
            _error.WriteLine("  UnityAssetsPatcher inspect <assets-file> [--limit <count> | --all]");
            _error.WriteLine("  UnityAssetsPatcher inspect <assets-file> <path-id> --detail");
            _error.WriteLine("  UnityAssetsPatcher find <assets-file> --config <json-path>");
            return 1;
        }

        try
        {
            if (IsFindCommand(args))
            {
                return PrintFindResults(args);
            }

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

    private static bool IsSupportedCommand(string[] args)
    {
        return IsInspectCommand(args) || IsFindCommand(args);
    }

    private static bool IsInspectCommand(string[] args)
    {
        if (args.Length == 2 || IsInspectSummaryOption(args))
        {
            return string.Equals(args[0], "inspect", StringComparison.OrdinalIgnoreCase);
        }

        return IsInspectDetailCommand(args);
    }

    private static bool IsFindCommand(string[] args)
    {
        return args.Length == 4
               && string.Equals(args[0], "find", StringComparison.OrdinalIgnoreCase)
               && string.Equals(args[2], "--config", StringComparison.OrdinalIgnoreCase);
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

    private int PrintFindResults(string[] args)
    {
        AssetQueryConfig queryConfig = LoadAssetQueryConfig(args[3]);
        var assets = _assetsReader.ReadAssetsInfo(args[1])
            .Where(asset => string.Equals(asset.TypeName, queryConfig.Type, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var matches = new List<(AssetsInfo Asset, IReadOnlyDictionary<string, JsonElement> IncludeGroup)>();

        foreach (AssetsInfo asset in assets)
        {
            AssetsFieldInfo fieldTree = _assetsReader.ReadAssetsFieldInfo(args[1], asset.PathId);
            var includeGroup = queryConfig.IncludeGroups
                .FirstOrDefault(group => MatchesIncludeGroup(fieldTree, group));

            if (includeGroup is not null)
            {
                matches.Add((asset, includeGroup));
            }
        }

        _output.WriteLine($"{"Path ID",12} | {"Type ID",7} | {"Type Name",-24} | Matched Fields");
        _output.WriteLine(new string('-', 86));

        foreach ((AssetsInfo asset, var includeGroup) in matches)
        {
            string matchedFields = string.Join(", ",
                includeGroup.Select(condition => $"{condition.Key}={FormatJsonValue(condition.Value)}"));
            _output.WriteLine($"{asset.PathId,12} | {asset.TypeId,7} | {asset.TypeName,-24} | {matchedFields}");
        }

        return 0;
    }

    private static AssetQueryConfig LoadAssetQueryConfig(string configPath)
    {
        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException($"Query config file not found: {configPath}", configPath);
        }

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(configPath));
        JsonElement root = document.RootElement;

        string type = root.TryGetProperty("type", out JsonElement typeElement) &&
                      typeElement.ValueKind == JsonValueKind.String
            ? typeElement.GetString() ?? throw new InvalidOperationException("Query config type cannot be empty.")
            : throw new InvalidOperationException("Query config must contain a string 'type' property.");

        if (!root.TryGetProperty("include", out JsonElement includeElement) ||
            includeElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Query config must contain an 'include' array.");
        }

        var includeGroups = new List<IReadOnlyDictionary<string, JsonElement>>();

        foreach (JsonElement includeGroupElement in includeElement.EnumerateArray())
        {
            if (includeGroupElement.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException("Each include entry must be an object.");
            }

            includeGroups.Add(includeGroupElement.EnumerateObject()
                .ToDictionary(property => property.Name, property => property.Value.Clone(), StringComparer.Ordinal));
        }

        return includeGroups.Count == 0
            ? throw new InvalidOperationException("Query config include array cannot be empty.")
            : new AssetQueryConfig(type, includeGroups);
    }

    private static bool MatchesIncludeGroup(AssetsFieldInfo fieldTree,
        IReadOnlyDictionary<string, JsonElement> includeGroup)
    {
        foreach ((string path, JsonElement expectedValue) in includeGroup)
        {
            AssetsFieldInfo? field = FindField(fieldTree, path);

            if (field?.Value is null || !MatchesValue(field.Value, expectedValue))
            {
                return false;
            }
        }

        return true;
    }

    private static AssetsFieldInfo? FindField(AssetsFieldInfo fieldTree, string path)
    {
        if (!path.Contains('.', StringComparison.Ordinal))
        {
            return FindDescendantByName(fieldTree, path);
        }

        AssetsFieldInfo? current = fieldTree;

        foreach (string segment in path.Split('.'))
        {
            current = current.Children.FirstOrDefault(child =>
                string.Equals(child.Name, segment, StringComparison.Ordinal));

            if (current is null)
            {
                return null;
            }
        }

        return current;
    }

    private static AssetsFieldInfo? FindDescendantByName(AssetsFieldInfo field, string name)
    {
        if (string.Equals(field.Name, name, StringComparison.Ordinal))
        {
            return field;
        }

        return field.Children
            .Select(child => FindDescendantByName(child, name))
            .OfType<AssetsFieldInfo>()
            .FirstOrDefault();
    }

    private static bool MatchesValue(string actualValue, JsonElement expectedValue)
    {
        return expectedValue.ValueKind switch
        {
            JsonValueKind.Number => MatchesNumber(actualValue, expectedValue),
            JsonValueKind.True => MatchesBoolean(actualValue, true),
            JsonValueKind.False => MatchesBoolean(actualValue, false),
            JsonValueKind.String => string.Equals(actualValue, expectedValue.GetString(), StringComparison.Ordinal),
            _ => false,
        };
    }

    private static bool MatchesNumber(string actualValue, JsonElement expectedValue)
    {
        return double.TryParse(actualValue, NumberStyles.Float, CultureInfo.InvariantCulture, out double actualNumber)
               && expectedValue.TryGetDouble(out double expectedNumber)
               && Math.Abs(actualNumber - expectedNumber) <= 0.00001d;
    }

    private static bool MatchesBoolean(string actualValue, bool expectedValue)
    {
        if (bool.TryParse(actualValue, out bool actualBoolean))
        {
            return actualBoolean == expectedValue;
        }

        if (long.TryParse(actualValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out long actualInteger))
        {
            return actualInteger != 0 == expectedValue;
        }

        return false;
    }

    private static string FormatJsonValue(JsonElement value)
    {
        return value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.GetRawText();
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

    private sealed record AssetQueryConfig(
        string Type,
        IReadOnlyList<IReadOnlyDictionary<string, JsonElement>> IncludeGroups);
}

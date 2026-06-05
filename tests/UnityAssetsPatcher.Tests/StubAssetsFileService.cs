using UnityAssetsPatcher.Core.Assets;

namespace UnityAssetsPatcher.Tests;

internal sealed class StubAssetsFileService : IAssetsFileService
{
    private readonly IReadOnlyList<AssetsInfo> _result;
    private readonly IReadOnlyDictionary<long, AssetsFieldInfo> _fieldTrees;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<AssetsInfo>> _resultsByPath;
    private readonly IReadOnlyDictionary<(string AssetsFilePath, long PathId), AssetsFieldInfo> _fieldTreesByPath;

    public StubAssetsFileService(IReadOnlyList<AssetsInfo> result)
        : this(result, new Dictionary<long, AssetsFieldInfo>()) { }

    public StubAssetsFileService(
        IReadOnlyList<AssetsInfo> result,
        IReadOnlyDictionary<long, AssetsFieldInfo> fieldTrees)
    {
        _result = result;
        _fieldTrees = fieldTrees;
        _resultsByPath = new Dictionary<string, IReadOnlyList<AssetsInfo>>(StringComparer.OrdinalIgnoreCase);
        _fieldTreesByPath = new Dictionary<(string AssetsFilePath, long PathId), AssetsFieldInfo>();
    }

    public StubAssetsFileService(
        IReadOnlyDictionary<string, IReadOnlyList<AssetsInfo>> resultsByPath,
        IReadOnlyDictionary<(string AssetsFilePath, long PathId), AssetsFieldInfo> fieldTreesByPath)
    {
        _result = [];
        _fieldTrees = new Dictionary<long, AssetsFieldInfo>();
        _resultsByPath = resultsByPath;
        _fieldTreesByPath = fieldTreesByPath;
    }

    public bool WasCalled { get; private set; }
    public string? InputPath { get; private set; }
    public string? OutputPath { get; private set; }
    public long? ReceivedPathId { get; private set; }
    public IReadOnlyList<PatchWriteAsset> Plan { get; private set; } = [];
    public IReadOnlyList<AssetReplacement> ReplacementPlan { get; private set; } = [];

    public IReadOnlyList<AssetsInfo> ReadAssetsInfo(string assetsFilePath)
    {
        if (_resultsByPath.TryGetValue(assetsFilePath, out var result))
        {
            return result;
        }

        return _resultsByPath.TryGetValue(Path.GetFileName(assetsFilePath), out result)
            ? result
            : _result;
    }

    public AssetsFieldInfo ReadAssetsFieldInfo(string assetsFilePath, long pathId)
    {
        ReceivedPathId = pathId;

        if (_fieldTreesByPath.TryGetValue((assetsFilePath, pathId), out AssetsFieldInfo? fieldTreeByPath) ||
            _fieldTreesByPath.TryGetValue((Path.GetFileName(assetsFilePath), pathId), out fieldTreeByPath))
        {
            return fieldTreeByPath;
        }

        return _fieldTrees.TryGetValue(pathId, out AssetsFieldInfo? fieldTree)
            ? fieldTree
            : throw new InvalidOperationException("Field tree was not configured.");
    }

    public void WritePatch(string inputPath, string outputPath, IReadOnlyList<PatchWriteAsset> plan)
    {
        WasCalled = true;
        InputPath = inputPath;
        OutputPath = outputPath;
        Plan = plan;
        File.WriteAllText(outputPath, "patched");
    }

    public void WriteReplacements(string inputPath, string outputPath, IReadOnlyList<AssetReplacement> plan)
    {
        WasCalled = true;
        InputPath = inputPath;
        OutputPath = outputPath;
        ReplacementPlan = plan;
        File.WriteAllText(outputPath, "patched");
    }
}

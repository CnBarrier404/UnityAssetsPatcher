namespace UnityAssetsPatcher.Application.Modules;

public sealed record TargetAssetSet(IReadOnlyList<TargetAsset> Targets)
{
    public IReadOnlyList<string> AssetsFilePaths { get; } = Targets
        .Select(target => target.AssetsFilePath)
        .ToArray();
}

namespace UnityAssetsPatcher.Core.Assets;

public sealed record AssetReplacement(string SourceAssetsFilePath, long SourcePathId, long TargetPathId);

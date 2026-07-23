namespace UnityAssetsPatcher.Domain.Assets;

public sealed record AssetReplacement(string SourceAssetsFilePath, long SourcePathId, long TargetPathId);

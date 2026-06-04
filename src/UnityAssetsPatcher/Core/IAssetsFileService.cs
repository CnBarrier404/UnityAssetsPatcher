using System.Text.Json;

namespace UnityAssetsPatcher.Core;

public interface IAssetsFileService
{
    public IReadOnlyList<AssetsInfo> ReadAssetsInfo(string assetsFilePath);
    public AssetsFieldInfo ReadAssetsFieldInfo(string assetsFilePath, long pathId);
    public void WritePatch(string inputPath, string outputPath, IReadOnlyList<PatchWriteAsset> plan);
}

public sealed record PatchWriteAsset(long PathId, IReadOnlyList<PatchWriteOperation> Operations);

public sealed record PatchWriteOperation(string Path, string OldValue, JsonElement To);

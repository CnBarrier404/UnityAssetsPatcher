namespace UnityAssetsPatcher.Core;

public interface IAssetsPatchWriter
{
    void WritePatch(string inputPath, string outputPath, IReadOnlyList<PatchWriteAsset> plan);
}

public sealed record PatchWriteAsset(long PathId, IReadOnlyList<PatchWriteOperation> Operations);

public sealed record PatchWriteOperation(string Path, string OldValue, System.Text.Json.JsonElement To);

using UnityAssetsPatcher.Domain.Assets;

namespace UnityAssetsPatcher.Abstractions.Assets;

public interface IAssetsFileWriter
{
    public void WriteFieldPatches(string inputPath, string outputPath, IReadOnlyList<AssetFieldPatch> plan);
    public void WriteReplacements(string inputPath, string outputPath, IReadOnlyList<AssetReplacement> plan);

    public void WriteFieldPatchesAndCopies(
        string inputPath,
        string outputPath,
        IReadOnlyList<AssetFieldPatch> fieldPatches,
        IReadOnlyList<AssetCopy> copies);
}

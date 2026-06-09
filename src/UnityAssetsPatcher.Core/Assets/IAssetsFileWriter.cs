namespace UnityAssetsPatcher.Core.Assets;

public interface IAssetsFileWriter
{
    public void WritePatch(string inputPath, string outputPath, IReadOnlyList<AssetFieldPatch> plan);
    public void WriteReplacements(string inputPath, string outputPath, IReadOnlyList<AssetReplacement> plan);
}

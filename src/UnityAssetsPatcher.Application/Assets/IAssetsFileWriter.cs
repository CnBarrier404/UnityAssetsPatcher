namespace UnityAssetsPatcher.Application.Assets;

public interface IAssetsFileWriter : IDisposable
{
    public void WriteFieldPatches(string inputPath, string outputPath, IReadOnlyList<AssetFieldPatch> plan);
    public void WriteReplacements(string inputPath, string outputPath, IReadOnlyList<AssetReplacement> plan);
}

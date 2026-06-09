using UnityAssetsPatcher.Core.Assets;

namespace UnityAssetsPatcher.AssetsTools;

public sealed class AssetsFileService : IAssetsFileWriter
{
    private readonly AssetsFileWriter _writer;

    public AssetsFileService(string tpkFilePath)
    {
        _writer = new AssetsFileWriter(tpkFilePath);
    }

    public void WritePatch(string inputPath, string outputPath, IReadOnlyList<AssetFieldPatch> plan)
    {
        _writer.WritePatch(inputPath, outputPath, plan);
    }

    public void WriteReplacements(string inputPath, string outputPath, IReadOnlyList<AssetReplacement> plan)
    {
        _writer.WriteReplacements(inputPath, outputPath, plan);
    }
}

using UnityAssetsPatcher.Domain.Assets;

namespace UnityAssetsPatcher.Application.Assets;

public interface IAssetsFileReader
{
    public IReadOnlyList<AssetInfo> ReadAssets(string assetsFilePath);

    public AssetField ReadField(string assetsFilePath, long pathId);
}

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

public interface IAssetsAccessScope : IDisposable
{
    public IAssetsFileReader Reader { get; }

    public IAssetsFileWriter Writer { get; }
}

public interface IAssetsAccessScopeFactory
{
    public IAssetsAccessScope CreateScope();
}

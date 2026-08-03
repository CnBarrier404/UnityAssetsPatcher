using UnityAssetsPatcher.Application.Assets;
using UnityAssetsPatcher.Application.Patching;
using UnityAssetsPatcher.Domain.Assets;
using Xunit;

namespace UnityAssetsPatcher.Tests.Application.Patching;

public sealed class AssetQueryServiceTests
{
    [Fact]
    public void ReadField_WhenSamePathIdIsReadAgain_DoesNotRetainFieldTree()
    {
        var reader = new CreatingAssetsFileReader();
        var context = new AssetQueryContext(reader, "sharedassets0.assets");

        AssetField first = context.ReadField(1);
        AssetField second = context.ReadField(1);

        Assert.NotSame(first, second);
        Assert.Equal(2, reader.FieldReadCount);
    }

    private sealed class CreatingAssetsFileReader : IAssetsFileReader
    {
        public int FieldReadCount { get; private set; }

        public IReadOnlyList<AssetInfo> ReadAssets(string assetsFilePath)
        {
            return [new AssetInfo(1, "Material")];
        }

        public AssetField ReadField(string assetsFilePath, long pathId)
        {
            FieldReadCount++;

            return TestAssetField.Create(
                "Material",
                "Material",
                null,
                [TestAssetField.Create("m_Name", "string", new AssetFieldValue.String("Test"), [])]);
        }

        public void Dispose() { }
    }
}

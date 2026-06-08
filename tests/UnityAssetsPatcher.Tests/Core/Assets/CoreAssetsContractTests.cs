using UnityAssetsPatcher.Core.Assets;
using Xunit;

namespace UnityAssetsPatcher.Tests.Core.Assets;

public sealed class CoreAssetsContractTests
{
    [Fact]
    public void Contracts_DoNotExposeCombinedAssetsFileService()
    {
        Type[] coreTypes = typeof(IAssetsFileReader).Assembly.GetTypes();

        Assert.DoesNotContain(coreTypes, type => type.Name == "IAssetsFileService");
    }

    [Fact]
    public void Contracts_UseAssetsFilePortNames()
    {
        Type[] coreTypes = typeof(IAssetsFileReader).Assembly.GetTypes();
        string[] typeNames = coreTypes.Select(type => type.Name).ToArray();

        Assert.Contains("IAssetsFileReader", typeNames);
        Assert.Contains("IAssetsFileWriter", typeNames);
        Assert.DoesNotContain("IAssetsReader", typeNames);
        Assert.DoesNotContain("IAssetsPatchWriter", typeNames);
    }

    [Fact]
    public void FieldPatchOperation_DoesNotCarryPreviewOnlyOldValue()
    {
        string[] propertyNames = typeof(FieldPatchOperation)
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();

        Assert.DoesNotContain("OldValue", propertyNames);
    }

    [Fact]
    public void Contracts_UseFieldPatchPlanNames()
    {
        Type[] coreTypes = typeof(IAssetsFileReader).Assembly.GetTypes();
        string[] typeNames = coreTypes.Select(type => type.Name).ToArray();

        Assert.Contains("AssetFieldPatch", typeNames);
        Assert.Contains("FieldPatchOperation", typeNames);
        Assert.DoesNotContain("PatchWriteAsset", typeNames);
        Assert.DoesNotContain("PatchWriteOperation", typeNames);
    }
}

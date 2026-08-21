using AssetsTools.NET;
using Microsoft.Extensions.Logging.Abstractions;
using UnityAssetsPatcher.Domain.Assets;
using UnityAssetsPatcher.Infrastructure.AssetsTools;
using Xunit;

namespace UnityAssetsPatcher.Infrastructure.Tests.AssetsTools;

public sealed class AssetReplacementCompatibilityValidatorTests
{
    [Fact]
    public void ValidateMetadataAndAssetCompatibility_WhenUnityVersionsDiffer_DoesNotThrow()
    {
        ClassPackageCache classPackageCache = CreateClassPackageCache();
        using AssetsFileSession targetSession = OpenSession(classPackageCache);
        using AssetsFileSession sourceSession = OpenSession(classPackageCache);
        sourceSession.AssetsFile.Metadata.UnityVersion = "2019.4.40f1";

        AssetReplacementCompatibilityValidator.ValidateMetadataAndAssetCompatibility(
            targetSession,
            new AssetPathId(4),
            sourceSession,
            new AssetPathId(4));
    }

    [Fact]
    public void ValidateMetadataAndAssetCompatibility_WhenSerializationHeadersDiffer_Throws()
    {
        ClassPackageCache classPackageCache = CreateClassPackageCache();
        using AssetsFileSession targetSession = OpenSession(classPackageCache);
        using AssetsFileSession sourceSession = OpenSession(classPackageCache);
        sourceSession.AssetsFile.Header.Version++;

        var exception = Assert.Throws<InvalidOperationException>(() =>
            AssetReplacementCompatibilityValidator.ValidateMetadataAndAssetCompatibility(
                targetSession,
                new AssetPathId(4),
                sourceSession,
                new AssetPathId(4)));

        Assert.Contains("serialization header", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateMetadataAndAssetCompatibility_WhenTypeTreeModesDiffer_Throws()
    {
        ClassPackageCache classPackageCache = CreateClassPackageCache();
        using AssetsFileSession targetSession = OpenSession(classPackageCache);
        using AssetsFileSession sourceSession = OpenSession(classPackageCache);
        sourceSession.AssetsFile.Metadata.TypeTreeEnabled = !targetSession.AssetsFile.Metadata.TypeTreeEnabled;

        var exception = Assert.Throws<InvalidOperationException>(() =>
            AssetReplacementCompatibilityValidator.ValidateMetadataAndAssetCompatibility(
                targetSession,
                new AssetPathId(4),
                sourceSession,
                new AssetPathId(4)));

        Assert.Contains("TypeTree mode", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateMetadataAndAssetCompatibility_WhenEffectiveTypeIdsDiffer_Throws()
    {
        ClassPackageCache classPackageCache = CreateClassPackageCache();
        using AssetsFileSession targetSession = OpenSession(classPackageCache);
        using AssetsFileSession sourceSession = OpenSession(classPackageCache);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            AssetReplacementCompatibilityValidator.ValidateMetadataAndAssetCompatibility(
                targetSession,
                new AssetPathId(4),
                sourceSession,
                new AssetPathId(1)));

        Assert.Contains("Type ID", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateFields_WhenSerializedFieldLayoutsDiffer_Throws()
    {
        ClassPackageCache classPackageCache = CreateClassPackageCache();
        using AssetsFileSession targetSession = OpenSession(classPackageCache);
        using AssetsFileSession sourceSession = OpenSession(classPackageCache);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            AssetReplacementCompatibilityValidator.ValidateFields(
                targetSession,
                new AssetPathId(4),
                CreateRootWithStringField("m_Name"),
                sourceSession,
                new AssetPathId(4),
                CreateRootWithStringField("m_Content")));

        Assert.Contains("serialized field", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateFields_WhenSourceContainsExternalPointer_Throws()
    {
        ClassPackageCache classPackageCache = CreateClassPackageCache();
        using AssetsFileSession targetSession = OpenSession(classPackageCache);
        using AssetsFileSession sourceSession = OpenSession(classPackageCache);
        AssetTypeValueField sourceField = CreateRootWithPointer(1, 0);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            AssetReplacementCompatibilityValidator.ValidateFields(
                targetSession,
                new AssetPathId(4),
                sourceField.Clone(),
                sourceSession,
                new AssetPathId(4),
                sourceField));

        Assert.Contains("external PPtr", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateFields_WhenLocalPointerPathIdIsMissing_Throws()
    {
        ClassPackageCache classPackageCache = CreateClassPackageCache();
        using AssetsFileSession targetSession = OpenSession(classPackageCache);
        using AssetsFileSession sourceSession = OpenSession(classPackageCache);
        AssetTypeValueField sourceField = CreateRootWithPointer(0, long.MaxValue);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            AssetReplacementCompatibilityValidator.ValidateFields(
                targetSession,
                new AssetPathId(4),
                sourceField.Clone(),
                sourceSession,
                new AssetPathId(4),
                sourceField));

        Assert.Contains("not present in both source and target files", exception.Message, StringComparison.Ordinal);
    }

    private static AssetTypeValueField CreateRootWithStringField(string name)
    {
        return TestAssetFieldFactory.Object(
            "Base",
            "TextAsset",
            TestAssetFieldFactory.Scalar(name, "string", new AssetTypeValue("value")));
    }

    private static AssetTypeValueField CreateRootWithPointer(int fileId, long pathId)
    {
        AssetTypeValueField pointer = TestAssetFieldFactory.Object(
            "m_Reference",
            "PPtr<Object>",
            TestAssetFieldFactory.Scalar("m_FileID", "int", new AssetTypeValue(fileId)),
            TestAssetFieldFactory.Scalar("m_PathID", "SInt64", new AssetTypeValue(pathId)));

        return TestAssetFieldFactory.Object("Base", "TextAsset", pointer);
    }

    private static ClassPackageCache CreateClassPackageCache()
    {
        return new ClassPackageCache(
            () => File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures", "resources.tpk")),
            NullLogger<ClassPackageCache>.Instance);
    }

    private static AssetsFileSession OpenSession(ClassPackageCache classPackageCache)
    {
        return AssetsFileSession.Open(GetAssetsFilePath(), classPackageCache);
    }

    private static string GetAssetsFilePath()
    {
        return Path.Combine(AppContext.BaseDirectory, "Fixtures", "sharedassets0.assets");
    }
}

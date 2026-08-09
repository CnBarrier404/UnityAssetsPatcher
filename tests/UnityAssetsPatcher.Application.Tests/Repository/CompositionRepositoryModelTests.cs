using UnityAssetsPatcher.Application.Repository;
using UnityAssetsPatcher.Domain.Integrity;
using Xunit;

namespace UnityAssetsPatcher.Application.Tests.Repository;

public sealed class CompositionRepositoryModelTests
{
    [Fact]
    public void BaseCatalog_WhenAssetsCollectionIsNull_ThrowsArgumentNullException()
    {
        FileIntegrity integrity = CreateIntegrity();

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => new BaseCatalog(
            "game-fingerprint",
            DateTimeOffset.UnixEpoch,
            null!,
            [new PayloadBaseEntry("Data/config.txt", PayloadBaseState.Absent)]));

        Assert.Equal("assetsFiles", exception.ParamName);
    }

    [Fact]
    public void BaseCatalog_WhenCollectionContainsNull_ThrowsArgumentException()
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(() => new BaseCatalog(
            "game-fingerprint",
            DateTimeOffset.UnixEpoch,
            [null!],
            []));

        Assert.Equal("assetsFiles", exception.ParamName);
    }

    [Fact]
    public void BaseCatalog_WhenCollectionIsEmpty_PreservesEmptyCollections()
    {
        BaseCatalog catalog = new(
            "game-fingerprint",
            DateTimeOffset.UnixEpoch,
            [],
            []);

        Assert.Empty(catalog.AssetsFiles);
        Assert.Empty(catalog.PayloadTargets);
    }

    [Theory]
    [InlineData("../outside.assets")]
    [InlineData("C:/outside.assets")]
    [InlineData("\\\\server\\share\\outside.assets")]
    public void BaseFileEntry_WhenRelativePathIsUnsafe_ThrowsArgumentException(string relativePath)
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(() => new BaseFileEntry(
            relativePath,
            CreateIntegrity()));

        Assert.Equal("relativePath", exception.ParamName);
    }

    [Fact]
    public void BaseCatalog_WhenAssetsPathsContainDuplicates_ThrowsArgumentException()
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(() => new BaseCatalog(
            "game-fingerprint",
            DateTimeOffset.UnixEpoch,
            [
                new BaseFileEntry("Game_Data/sharedassets0.assets", CreateIntegrity()),
                new BaseFileEntry("Game_Data/sharedassets0.assets", CreateIntegrity()),
            ],
            []));

        Assert.Equal("assetsFiles", exception.ParamName);
    }

    [Fact]
    public void PayloadBaseEntry_WhenPresentWithoutIntegrity_ThrowsArgumentNullException()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => new PayloadBaseEntry(
            "Data/config.txt",
            PayloadBaseState.Present));

        Assert.Equal("integrity", exception.ParamName);
    }

    [Fact]
    public void PayloadBaseEntry_WhenAbsentWithIntegrity_ThrowsArgumentException()
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(() => new PayloadBaseEntry(
            "Data/config.txt",
            PayloadBaseState.Absent,
            CreateIntegrity()));

        Assert.Equal("integrity", exception.ParamName);
    }

    [Fact]
    public void LayerRecord_WhenInstallSequenceIsNotPositive_ThrowsArgumentOutOfRangeException()
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() => new LayerRecord(
            "repository",
            "game-fingerprint",
            0,
            "layer-1",
            DateTimeOffset.UnixEpoch,
            "Test Mod",
            "1.0.0",
            "Test Author",
            "Test Game",
            null,
            true,
            new LayerPackageInfo("package.zip", CreateIntegrity()),
            [],
            []));

        Assert.Equal("installSequence", exception.ParamName);
    }

    [Fact]
    public void LayerRecord_WhenAssetsAndPayloadTargetsOverlap_ThrowsArgumentException()
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(() => new LayerRecord(
            "repository",
            "game-fingerprint",
            1,
            "layer-1",
            DateTimeOffset.UnixEpoch,
            "Test Mod",
            "1.0.0",
            "Test Author",
            "Test Game",
            null,
            true,
            new LayerPackageInfo("package.zip", CreateIntegrity()),
            ["Game_Data/sharedassets0.assets"],
            ["Game_Data/sharedassets0.assets"]));

        Assert.Equal("payloadTargets", exception.ParamName);
    }

    [Fact]
    public void LayerPackageInfo_WhenFileNameContainsDirectory_ThrowsArgumentException()
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(() => new LayerPackageInfo(
            "packages/package.zip",
            CreateIntegrity()));

        Assert.Equal("fileName", exception.ParamName);
    }

    private static FileIntegrity CreateIntegrity()
    {
        return FileIntegrity.Create("contents"u8);
    }
}

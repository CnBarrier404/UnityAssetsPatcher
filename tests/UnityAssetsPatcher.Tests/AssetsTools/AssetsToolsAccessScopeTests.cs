using UnityAssetsPatcher.AssetsTools;
using UnityAssetsPatcher.Core.Assets;
using Xunit;

namespace UnityAssetsPatcher.Tests.AssetsTools;

public sealed class AssetsToolsAccessScopeTests
{
    [Fact]
    public void CreateScope_WhenScopesOpenAssetsFiles_ReturnsAssetData()
    {
        using var factory = new AssetsToolsAccessScopeFactory(GetRealTpkFilePath());

        using (IAssetsAccessScope scope = factory.CreateScope())
        {
            var assets = scope.Reader.ReadAssetsInfo(GetRealAssetsFilePath());
            Assert.NotEmpty(assets);
        }

        using (IAssetsAccessScope scope = factory.CreateScope())
        {
            var assets = scope.Reader.ReadAssetsInfo(GetRealAssetsFilePath());
            Assert.NotEmpty(assets);
        }
    }

    [Fact]
    public void ReadAssetsFieldInfo_WhenReadingMultipleAssetsInOneSession_ReturnsFieldTrees()
    {
        using var factory = new AssetsToolsAccessScopeFactory(GetRealTpkFilePath());
        using IAssetsAccessScope scope = factory.CreateScope();
        var assets = scope.Reader.ReadAssetsInfo(GetRealAssetsFilePath())
            .Take(2)
            .ToArray();
        Assert.Equal(2, assets.Length);

        AssetsFieldInfo firstFieldTree = scope.Reader.ReadAssetsFieldInfo(GetRealAssetsFilePath(), assets[0].PathId);
        AssetsFieldInfo secondFieldTree = scope.Reader.ReadAssetsFieldInfo(GetRealAssetsFilePath(), assets[1].PathId);

        Assert.False(string.IsNullOrWhiteSpace(firstFieldTree.Name));
        Assert.False(string.IsNullOrWhiteSpace(secondFieldTree.Name));
    }

    [Fact]
    public void CloseReadSessions_WhenReaderIsUsedAgain_OpensANewReadableSession()
    {
        using var factory = new AssetsToolsAccessScopeFactory(GetRealTpkFilePath());
        using IAssetsAccessScope scope = factory.CreateScope();

        Assert.NotEmpty(scope.Reader.ReadAssetsInfo(GetRealAssetsFilePath()));

        scope.CloseReadSessions();

        Assert.NotEmpty(scope.Reader.ReadAssetsInfo(GetRealAssetsFilePath()));
    }

    [Fact]
    public void Dispose_WhenReaderIsUsedAgain_ThrowsObjectDisposedException()
    {
        using var context = new AssetsToolsContext(GetRealTpkFilePath());
        var reader = new AssetsFileReader(context, ownsContext: false);
        reader.Dispose();

        Assert.Throws<ObjectDisposedException>(() => reader.ReadAssetsInfo(GetRealAssetsFilePath()));
    }

    [Fact]
    public void CloseReadSessions_WhenCalledMultipleTimes_ForwardsEveryCallWithoutDisposingReader()
    {
        var reader = new DisposableAssetsReader();
        var writer = new DisposableAssetsWriter();
        using var scope = new AssetsToolsAccessScope(reader, writer);

        scope.CloseReadSessions();
        scope.CloseReadSessions();

        Assert.Equal(2, reader.CloseReadSessionsCount);
        Assert.Equal(0, reader.DisposeCount);
        Assert.Equal(0, writer.DisposeCount);
    }

    [Fact]
    public void Dispose_ReleasesReaderAndWriter()
    {
        var reader = new DisposableAssetsReader();
        var writer = new DisposableAssetsWriter();
        var scope = new AssetsToolsAccessScope(reader, writer);

        scope.Dispose();

        Assert.Equal(1, reader.DisposeCount);
        Assert.Equal(1, writer.DisposeCount);
    }

    private sealed class DisposableAssetsReader : IAssetsFileReader, IDisposable
    {
        public int DisposeCount { get; private set; }
        public int CloseReadSessionsCount { get; private set; }

        public IReadOnlyList<AssetsInfo> ReadAssetsInfo(string assetsFilePath)
        {
            throw new NotSupportedException();
        }

        public AssetsFieldInfo ReadAssetsFieldInfo(string assetsFilePath, long pathId)
        {
            throw new NotSupportedException();
        }

        public void CloseReadSessions()
        {
            CloseReadSessionsCount++;
        }

        public void Dispose()
        {
            DisposeCount++;
        }
    }

    private sealed class DisposableAssetsWriter : IAssetsFileWriter, IDisposable
    {
        public int DisposeCount { get; private set; }

        public void WritePatch(string inputPath, string outputPath, IReadOnlyList<AssetFieldPatch> plan)
        {
            throw new NotSupportedException();
        }

        public void WriteReplacements(string inputPath, string outputPath, IReadOnlyList<AssetReplacement> plan)
        {
            throw new NotSupportedException();
        }

        public void Dispose()
        {
            DisposeCount++;
        }
    }

    private static string FindRepositoryRoot()
    {
        string? directory = Directory.GetCurrentDirectory();

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory, "UnityAssetsPatcher.sln")))
            {
                return directory;
            }

            directory = Directory.GetParent(directory)?.FullName;
        }

        throw new InvalidOperationException("Repository root was not found.");
    }

    private static string GetRealAssetsFilePath()
    {
        return Path.Combine(
            FindRepositoryRoot(),
            "tests",
            "UnityAssetsPatcher.Tests",
            "RealTestAssets",
            "sharedassets0.assets");
    }

    private static string GetRealTpkFilePath()
    {
        return Path.Combine(
            FindRepositoryRoot(),
            "src",
            "UnityAssetsPatcher",
            "Assets",
            "AssetsRipper.tpk");
    }
}

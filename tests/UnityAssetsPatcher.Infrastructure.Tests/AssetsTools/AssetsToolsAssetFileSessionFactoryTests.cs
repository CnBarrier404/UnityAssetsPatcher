using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using UnityAssetsPatcher.Application.Assets;
using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Domain.Assets;
using UnityAssetsPatcher.Domain.Integrity;
using UnityAssetsPatcher.Infrastructure.AssetsTools;
using Xunit;

namespace UnityAssetsPatcher.Infrastructure.Tests.AssetsTools;

public sealed class AssetsToolsAssetFileSessionFactoryTests
{
    [Fact]
    public void AddUnityAssetsPatcherInfrastructure_WhenDependenciesAreRegistered_ResolvesSingletonFactory()
    {
        using ServiceProvider provider = new ServiceCollection()
            .AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance)
            .AddSingleton<IFileSystemOperations>(new TestFileSystemOperations())
            .AddUnityAssetsPatcherInfrastructure(OpenClassPackage)
            .BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true,
            });

        IAssetFileSessionFactory first = provider.GetRequiredService<IAssetFileSessionFactory>();
        IAssetFileSessionFactory second = provider.GetRequiredService<IAssetFileSessionFactory>();

        Assert.IsType<AssetsToolsAssetFileSessionFactory>(first);
        Assert.Same(first, second);
    }

    [Fact]
    public void ReadAssetsAndField_WhenFactoryIsReused_ReturnsMappedDataAndReusesClassPackage()
    {
        int classPackageOpenCount = 0;
        var factory = CreateFactory(() =>
        {
            classPackageOpenCount++;

            return OpenClassPackage();
        });

        using IAssetFileSession firstSession = factory.Open(GetAssetsFilePath());
        IReadOnlyList<AssetInfo> assets = firstSession.ReadAssets();
        AssetField textAsset = firstSession.ReadField(new AssetPathId(4));
        using IAssetFileSession secondSession = factory.Open(GetAssetsFilePath());
        _ = secondSession.ReadField(new AssetPathId(1));

        Assert.Contains(new AssetInfo(new AssetPathId(4), "TextAsset"), assets);
        AssetScalarField name = Assert.IsType<AssetScalarField>(FindDescendant(textAsset, "m_Name"));
        Assert.Equal(new AssetScalarValue.String("Text"), name.Value);
        Assert.Equal(1, classPackageOpenCount);
    }

    [Fact]
    public void ReadField_WhenPathIdDoesNotExist_ThrowsInvalidOperationException()
    {
        var factory = CreateFactory();
        using IAssetFileSession session = factory.Open(GetAssetsFilePath());

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => session.ReadField(new AssetPathId(long.MaxValue)));

        Assert.Equal($"Asset not found or cannot be read: {long.MaxValue}", exception.Message);
    }

    [Fact]
    public void Write_WhenFieldIsPatched_WritesReopenableOutputAndPreservesInput()
    {
        string outputRoot = CreateTemporaryDirectoryPath();
        string outputPath = Path.Combine(outputRoot, "patched.assets");
        var factory = CreateFactory();

        try
        {
            using (IAssetFileSession writeSession = factory.Open(GetAssetsFilePath()))
            {
                writeSession.Write(outputPath, CreateNamePatchPlan("Changed"));
            }

            using IAssetFileSession outputSession = factory.Open(outputPath);
            using IAssetFileSession inputSession = factory.Open(GetAssetsFilePath());

            Assert.Equal("Changed", ReadName(outputSession));
            Assert.Equal("Text", ReadName(inputSession));
        }
        finally
        {
            DeleteTemporaryDirectory(outputRoot);
        }
    }

    [Fact]
    public void Write_WhenAssetIsReplaced_WritesSourceAssetData()
    {
        string outputRoot = CreateTemporaryDirectoryPath();
        string sourcePath = Path.Combine(outputRoot, "source.assets");
        string outputPath = Path.Combine(outputRoot, "replaced.assets");
        var factory = CreateFactory();

        try
        {
            using (IAssetFileSession sourceSession = factory.Open(GetAssetsFilePath()))
            {
                sourceSession.Write(sourcePath, CreateNamePatchPlan("Replacement"));
            }

            using (IAssetFileSession replacementSession = factory.Open(GetAssetsFilePath()))
            {
                replacementSession.Write(outputPath, new AssetMutationPlan(
                [
                    new ReplaceAsset(new AssetSource(sourcePath, new AssetPathId(4)), new AssetPathId(4)),
                ]));
            }

            using IAssetFileSession outputSession = factory.Open(outputPath);

            Assert.Equal("Replacement", ReadName(outputSession));
        }
        finally
        {
            DeleteTemporaryDirectory(outputRoot);
        }
    }

    private static AssetsToolsAssetFileSessionFactory CreateFactory(
        Func<Stream>? openClassPackage = null,
        ILoggerFactory? loggerFactory = null)
    {
        return new AssetsToolsAssetFileSessionFactory(
            openClassPackage ?? OpenClassPackage,
            new TestFileSystemOperations(),
            loggerFactory ?? NullLoggerFactory.Instance);
    }

    private static AssetMutationPlan CreateNamePatchPlan(string value)
    {
        return new AssetMutationPlan(
        [
            new PatchAssetFields(new AssetPathId(4),
            [
                new SetAssetField(
                    new AssetFieldPath("m_Name"),
                    new AssetScalarWriteValue(new AssetScalarValue.String(value))),
            ]),
        ]);
    }

    private static string ReadName(IAssetFileSession session)
    {
        AssetField root = session.ReadField(new AssetPathId(4));
        AssetScalarField field = Assert.IsType<AssetScalarField>(FindDescendant(root, "m_Name"));
        AssetScalarValue.String value = Assert.IsType<AssetScalarValue.String>(field.Value);

        return value.Value;
    }

    private static AssetField FindDescendant(AssetField root, string name)
    {
        if (string.Equals(root.Name, name, StringComparison.Ordinal))
        {
            return root;
        }

        foreach (AssetField child in root.Children)
        {
            AssetField? match = FindDescendantOrDefault(child, name);

            if (match is not null)
            {
                return match;
            }
        }

        throw new InvalidOperationException($"Field not found: {name}");
    }

    private static AssetField? FindDescendantOrDefault(AssetField root, string name)
    {
        if (string.Equals(root.Name, name, StringComparison.Ordinal))
        {
            return root;
        }

        foreach (AssetField child in root.Children)
        {
            AssetField? match = FindDescendantOrDefault(child, name);

            if (match is not null)
            {
                return match;
            }
        }

        return null;
    }

    private static string GetAssetsFilePath()
    {
        return Path.Combine(AppContext.BaseDirectory, "Fixtures", "sharedassets0.assets");
    }

    private static Stream OpenClassPackage()
    {
        return File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures", "resources.tpk"));
    }

    private static string CreateTemporaryDirectoryPath()
    {
        return Path.Combine(Path.GetTempPath(), $"UnityAssetsPatcher-{Guid.NewGuid():N}");
    }

    private static void DeleteTemporaryDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private sealed class TestFileSystemOperations : IFileSystemOperations
    {
        public Stream OpenRead(string path)
        {
            return File.OpenRead(path);
        }

        public FileIntegrity ComputeFileIntegrity(string path)
        {
            throw new NotSupportedException();
        }

        public FileAttributes GetAttributes(string path)
        {
            throw new NotSupportedException();
        }

        public void WriteFileAtomically(string destinationPath, FileDestinationMode mode, Action<Stream> writer)
        {
            string? directory = Path.GetDirectoryName(destinationPath);

            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using FileStream stream = File.Create(destinationPath);
            writer(stream);
        }

        public void EnsureDirectory(string path)
        {
            Directory.CreateDirectory(path);
        }

        public void CopyFileAtomically(string sourcePath, string destinationPath, FileDestinationMode mode)
        {
            throw new NotSupportedException();
        }

        public void DeleteFile(string path)
        {
            throw new NotSupportedException();
        }

        public void MoveDirectory(string sourcePath, string destinationPath)
        {
            throw new NotSupportedException();
        }

        public void DeleteDirectoryTree(string path)
        {
            throw new NotSupportedException();
        }
    }
}

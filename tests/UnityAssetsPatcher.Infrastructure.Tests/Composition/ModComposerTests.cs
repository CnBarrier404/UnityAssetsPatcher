using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using UnityAssetsPatcher.Application;
using UnityAssetsPatcher.Application.Assets;
using UnityAssetsPatcher.Application.Repository;
using UnityAssetsPatcher.Application.Composition;
using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Application.Patching;
using UnityAssetsPatcher.Domain.Assets;
using UnityAssetsPatcher.Domain.Integrity;
using UnityAssetsPatcher.Infrastructure.Tests.Repository;
using Xunit;

namespace UnityAssetsPatcher.Infrastructure.Tests.Composition;

public sealed class ModComposerTests
{
    [Fact]
    public async Task ComposeAsync_WhenSingleLayerPatchesField_ReplaysBaseIntoWorkingCopyAndReResolvesTarget()
    {
        using CompositionTestFixture fixture = new();
        LayerRecord layer = fixture.AddLayer(
            "layer-one",
            1,
            CreateFieldManifest("Layer One"),
            [],
            ["Game_Data/stale.assets"],
            []);
        CompositionRequest request = fixture.CreateRequest(
            [layer],
            null,
            [new CompositionFileTarget(RepositoryFileKind.Assets, fixture.AssetsRelativePath)]);

        CompositionOutcome outcome = await fixture.ComposeAsync(request);

        var succeeded = Assert.IsType<CompositionSucceeded>(outcome);
        CompositionFileResult result = Assert.Single(succeeded.Result.Files);

        Assert.False(result.DeletesFile);
        Assert.NotEqual(fixture.GameAssetsPath, result.PreparedPath);
        Assert.Equal("Layer One", fixture.ReadName(result.PreparedPath!));
        Assert.Equal("Text", fixture.ReadName(fixture.GameAssetsPath));
    }

    [Fact]
    public async Task ComposeAsync_WhenTwoLayersPatchAndReplace_ReplaysBothLayersInOrder()
    {
        using CompositionTestFixture fixture = new();
        LayerRecord firstLayer = fixture.AddLayer(
            "layer-field",
            1,
            CreateFieldManifest("Layer One"),
            [],
            [fixture.AssetsRelativePath],
            []);
        string replacementPath = fixture.CreateReplacementAssets("Layer One", "Replacement Content");
        LayerRecord secondLayer = fixture.AddLayer(
            "layer-replacement",
            2,
            CreateReplacementManifest("Layer One", "replacement.assets"),
            [("replacement.assets", File.ReadAllBytes(replacementPath))],
            [fixture.AssetsRelativePath],
            []);
        CompositionRequest request = fixture.CreateRequest(
            [firstLayer, secondLayer],
            null,
            [new CompositionFileTarget(RepositoryFileKind.Assets, fixture.AssetsRelativePath)]);

        CompositionOutcome outcome = await fixture.ComposeAsync(request);

        var succeeded = Assert.IsType<CompositionSucceeded>(outcome);
        string preparedPath = Assert.Single(succeeded.Result.Files).PreparedPath!;

        Assert.Equal("Layer One", fixture.ReadName(preparedPath));
        Assert.Equal("Replacement Content", fixture.ReadStringField(preparedPath, fixture.ReplacementFieldPath));
    }

    [Fact]
    public async Task ComposeAsync_WhenUpperLayerExpectedValueDoesNotMatch_ReturnsValueMismatchForUpperLayer()
    {
        using CompositionTestFixture fixture = new();
        LayerRecord firstLayer = fixture.AddLayer(
            "layer-one",
            1,
            CreateFieldManifest("Layer One"),
            [],
            [fixture.AssetsRelativePath],
            []);
        LayerRecord secondLayer = fixture.AddLayer(
            "layer-two",
            2,
            CreateFieldManifest("Layer Two", "Text", "Layer One"),
            [],
            [fixture.AssetsRelativePath],
            []);
        CompositionRequest request = fixture.CreateRequest(
            [firstLayer, secondLayer],
            null,
            [new CompositionFileTarget(RepositoryFileKind.Assets, fixture.AssetsRelativePath)]);

        CompositionOutcome outcome = await fixture.ComposeAsync(request);

        var failed = Assert.IsType<CompositionFailed>(outcome);
        PatchDiagnostic diagnostic = Assert.Single(failed.Failure.Diagnostics);

        Assert.Equal("layer-two", failed.Failure.LayerId);
        Assert.Equal(fixture.AssetsRelativePath, failed.Failure.RelativePath);
        Assert.Equal(PatchDiagnosticCode.ValueMismatch, diagnostic.Code);
    }

    [Fact]
    public async Task ComposeAsync_WhenTopPayloadLayerIsExcluded_UsesFallbackAndRestoresBaseOrDeletesAbsentPayload()
    {
        using CompositionTestFixture fixture = new();
        LayerRecord fallbackLayer = fixture.AddLayer(
            "payload-fallback",
            1,
            CreatePayloadManifest("config.txt"),
            [("config.txt", "fallback"u8.ToArray())],
            [fixture.AssetsRelativePath],
            [fixture.PayloadRelativePath]);
        LayerRecord topLayer = fixture.AddLayer(
            "payload-top",
            2,
            CreatePayloadManifest("config.txt"),
            [("config.txt", "top"u8.ToArray())],
            [fixture.AssetsRelativePath],
            [fixture.PayloadRelativePath]);
        CompositionRequest request = fixture.CreateRequest(
            [fallbackLayer, topLayer],
            topLayer.Id,
            [
                new CompositionFileTarget(RepositoryFileKind.Payload, fixture.PayloadRelativePath),
                new CompositionFileTarget(RepositoryFileKind.Payload, fixture.BasePayloadRelativePath),
                new CompositionFileTarget(RepositoryFileKind.Payload, fixture.AbsentPayloadRelativePath)
            ]);

        CompositionOutcome outcome = await fixture.ComposeAsync(request);

        var succeeded = Assert.IsType<CompositionSucceeded>(outcome);
        CompositionFileResult[] results = [.. succeeded.Result.Files];

        Assert.Equal("fallback", File.ReadAllText(results[0].PreparedPath!));
        Assert.Equal("base", File.ReadAllText(results[1].PreparedPath!));
        Assert.True(results[2].DeletesFile);
    }

    [Fact]
    public async Task ComposeAsync_WhenLayerPackageIntegrityChanges_RejectsComposition()
    {
        using CompositionTestFixture fixture = new();
        LayerRecord layer = fixture.AddLayer(
            "layer-corrupt",
            1,
            CreateFieldManifest("Layer One"),
            [],
            [fixture.AssetsRelativePath],
            []);
        string packagePath = fixture.Repository.Layers.ResolvePackagePath(layer.Id);
        File.AppendAllText(packagePath, "corrupt");
        CompositionRequest request = fixture.CreateRequest(
            [layer],
            null,
            [new CompositionFileTarget(RepositoryFileKind.Assets, fixture.AssetsRelativePath)]);

        var exception = await Assert.ThrowsAnyAsync<Exception>(() => fixture.ComposeAsync(request));

        Assert.Equal("LayerPackageIntegrityException", exception.GetType().Name);
        Assert.Equal("Layer package integrity mismatch.", exception.Message);
    }

    private const string SchemaUri = "https://uap.cnbarrier.com/schema-v1.json";

    private static byte[] CreateFieldManifest(string to, string from = "Text", string match = "Text")
    {
        JsonObject patch = CreateTargetPatch(match);
        patch["set"] = new JsonObject
        {
            ["m_Name"] = new JsonObject
            {
                ["from"] = from,
                ["to"] = to
            }
        };

        return CreateManifest(patch);
    }

    private static byte[] CreateReplacementManifest(string targetName, string source)
    {
        JsonObject patch = CreateTargetPatch(targetName);
        patch["replaceAsset"] = new JsonObject
        {
            ["fromFile"] = source,
            ["matchField"] = "m_Name"
        };

        return CreateManifest(patch);
    }

    private static byte[] CreatePayloadManifest(string source)
    {
        JsonObject root = CreateManifestRoot();
        root["copyFiles"] = new JsonArray
        {
            new JsonObject
            {
                ["source"] = source
            }
        };
        root["targets"] = new JsonArray
        {
            new JsonObject
            {
                ["file"] = "sharedassets0.assets",
                ["patches"] = new JsonArray
                {
                    CreateTargetPatch()
                }
            }
        };

        return Encoding.UTF8.GetBytes(root.ToJsonString());
    }

    private static byte[] CreateManifest(JsonObject patch)
    {
        JsonObject root = CreateManifestRoot();
        root["targets"] = new JsonArray
        {
            new JsonObject
            {
                ["file"] = "sharedassets0.assets",
                ["patches"] = new JsonArray { patch }
            }
        };

        return Encoding.UTF8.GetBytes(root.ToJsonString());
    }

    private static JsonObject CreateManifestRoot()
    {
        return new JsonObject
        {
            ["$schema"] = SchemaUri,
            ["name"] = "Composition Test Mod",
            ["author"] = "Composition Tests",
            ["version"] = "1.0.0"
        };
    }

    private static JsonObject CreateTargetPatch(string name = "Text")
    {
        return new JsonObject
        {
            ["type"] = "TextAsset",
            ["match"] = new JsonObject
            {
                ["m_Name"] = name
            }
        };
    }

    private sealed class CompositionTestFixture : IDisposable
    {
        public string AssetsRelativePath { get; } = Path.Combine("Game_Data", "sharedassets0.assets");
        public string PayloadRelativePath { get; } = Path.Combine("Game_Data", "config.txt");
        public string BasePayloadRelativePath { get; } = Path.Combine("Game_Data", "base.txt");
        public string AbsentPayloadRelativePath { get; } = Path.Combine("Game_Data", "delete.txt");
        public string GameDirectory { get; }
        public string GameAssetsPath { get; }
        public string ReplacementFieldPath { get; }
        public ICompositionRepository Repository { get; }
        public IServiceProvider Provider { get; }

        private readonly RepositoryTestDirectory _directory;
        private readonly ServiceProvider _serviceProvider;

        public CompositionTestFixture()
        {
            _directory = new RepositoryTestDirectory();
            string repositoryDirectory = _directory.CreateDirectory("backup");
            _ = _directory.CreateDirectory("backup", ".temp");
            GameDirectory = _directory.CreateDirectory("game");
            string gameDataDirectory = _directory.CreateDirectory("game", "Game_Data");
            GameAssetsPath = Path.Combine(gameDataDirectory, "sharedassets0.assets");
            File.Copy(GetFixtureAssetsPath(), GameAssetsPath);
            _ = _directory.WriteFile(Path.Combine("game", "Game_Data", "base.txt"), "base");

            ServiceCollection services = new();
            services.AddLogging();
            services.AddUnityAssetsPatcherApplication();
            services.AddUnityAssetsPatcherOperations();
            services.AddUnityAssetsPatcherInfrastructure(OpenClassPackage);
            services.AddUnityAssetsPatcherRepository(repositoryDirectory);
            _serviceProvider = services.BuildServiceProvider();
            Provider = _serviceProvider;
            Repository = _serviceProvider.GetRequiredService<ICompositionRepository>();

            var fileSystem = _serviceProvider.GetRequiredService<IFileSystemOperations>();
            var capturer = _serviceProvider.GetRequiredService<BaseSnapshotCapturer>();
            using IRepositoryOperationLock operationLock =
                _serviceProvider.GetRequiredService<IRepositoryOperationLockProvider>().Acquire();
            _ = capturer.Capture(operationLock, GameDirectory, AssetsRelativePath, RepositoryFileKind.Assets);
            _ = capturer.Capture(operationLock, GameDirectory, BasePayloadRelativePath, RepositoryFileKind.Payload);
            _ = capturer.Capture(operationLock, GameDirectory, AbsentPayloadRelativePath, RepositoryFileKind.Payload);
            ReplacementFieldPath = FindReplacementFieldPath();
        }

        public CompositionRequest CreateRequest(
            IReadOnlyList<LayerRecord> layers,
            string? excludedLayerId,
            IReadOnlyList<CompositionFileTarget> files)
        {
            string workingDirectory = _directory.CreateDirectory("work", Guid.NewGuid().ToString("N"));

            return new CompositionRequest(GameDirectory, workingDirectory, layers, excludedLayerId, files);
        }

        public async Task<CompositionOutcome> ComposeAsync(CompositionRequest request)
        {
            using IServiceScope scope = _serviceProvider.CreateScope();
            var composer = scope.ServiceProvider.GetRequiredService<ModComposer>();

            return await composer.ComposeAsync(request, TestContext.Current.CancellationToken);
        }

        public LayerRecord AddLayer(
            string id,
            long sequence,
            byte[] manifest,
            (string Path, byte[] Content)[] entries,
            IReadOnlyList<string> assetsTargets,
            IReadOnlyList<string> payloadTargets)
        {
            string packageSourcePath = _directory.GetPath("packages", $"{id}.zip");
            CreatePackage(packageSourcePath, manifest, entries);
            var fileSystem = _serviceProvider.GetRequiredService<IFileSystemOperations>();
            FileIntegrity packageIntegrity = fileSystem.ComputeFileIntegrity(packageSourcePath);
            string fingerprint = GameInstanceIdentity.CreateFingerprint(fileSystem, GameDirectory);
            LayerRecord record = new(
                "repository",
                fingerprint,
                sequence,
                id,
                DateTimeOffset.UnixEpoch,
                "Composition Test Mod",
                "1.0.0",
                "Composition Tests",
                null,
                null,
                true,
                new LayerPackageInfo("package.zip", packageIntegrity),
                assetsTargets,
                payloadTargets);
            string preparedDirectory = _directory.CreateDirectory("backup", ".temp", id);
            Repository.Layers.StoreVerifiedPackage(packageSourcePath, preparedDirectory, record.Package);
            Repository.Layers.WritePreparedLayer(record, preparedDirectory);
            Repository.Layers.CommitLayer(preparedDirectory, record.Id);

            return record;
        }

        public string CreateReplacementAssets(string name, string replacementValue)
        {
            string path = _directory.GetPath("replacement", "replacement.assets");
            var factory = _serviceProvider.GetRequiredService<IAssetFileSessionFactory>();
            using IAssetFileSession session = factory.Open(GetFixtureAssetsPath());
            session.Write(path, new AssetMutationPlan(
            [
                new PatchAssetFields(
                    new AssetPathId(4),
                    [
                        new SetAssetField(
                            new AssetFieldPath("m_Name"),
                            new AssetScalarWriteValue(new AssetScalarValue.String(name))),
                        new SetAssetField(
                            new AssetFieldPath(ReplacementFieldPath),
                            new AssetScalarWriteValue(new AssetScalarValue.String(replacementValue)))
                    ])
            ]));

            return path;
        }

        public string ReadName(string path)
        {
            return ReadStringField(path, "m_Name");
        }

        public string ReadStringField(string path, string fieldPath)
        {
            var factory = _serviceProvider.GetRequiredService<IAssetFileSessionFactory>();
            using IAssetFileSession session = factory.Open(path);
            AssetField root = session.ReadField(new AssetPathId(4));
            AssetField field = FindField(root, fieldPath);
            var scalar = Assert.IsType<AssetScalarField>(field);
            var value = Assert.IsType<AssetScalarValue.String>(scalar.Value);

            return value.Value;
        }

        public void Dispose()
        {
            _serviceProvider.Dispose();
            _directory.Dispose();
        }

        private string FindReplacementFieldPath()
        {
            var factory = _serviceProvider.GetRequiredService<IAssetFileSessionFactory>();
            using IAssetFileSession session = factory.Open(GetFixtureAssetsPath());
            AssetField root = session.ReadField(new AssetPathId(4));
            string path = FindStringFieldPath(root);

            if (string.IsNullOrWhiteSpace(path))
            {
                throw new InvalidOperationException("The TextAsset fixture has no replaceable string field.");
            }

            return path;
        }

        private static string FindStringFieldPath(AssetField root)
        {
            foreach (AssetField child in root.Children)
            {
                string path = FindStringFieldPath(child, string.Empty);

                if (!string.IsNullOrEmpty(path))
                {
                    return path;
                }
            }

            return string.Empty;
        }

        private static string FindStringFieldPath(AssetField field, string prefix)
        {
            string currentPath = string.IsNullOrEmpty(prefix) ? field.Name : $"{prefix}.{field.Name}";

            if (!string.Equals(field.Name, "m_Name", StringComparison.Ordinal) &&
                field is AssetScalarField { Value: AssetScalarValue.String })
            {
                return currentPath;
            }

            foreach (AssetField child in field.Children)
            {
                string path = FindStringFieldPath(child, currentPath);

                if (!string.IsNullOrEmpty(path))
                {
                    return path;
                }
            }

            return string.Empty;
        }

        private static AssetField FindField(AssetField field, string path)
        {
            string[] segments = path.Split('.', StringSplitOptions.RemoveEmptyEntries);
            AssetField current = field;

            if (segments.Length > 0 && string.Equals(current.Name, segments[0], StringComparison.Ordinal))
            {
                segments = segments[1..];
            }

            foreach (string segment in segments)
            {
                current = current.Children.First(child =>
                    string.Equals(child.Name, segment, StringComparison.Ordinal));
            }

            return current;
        }

        private static void CreatePackage(
            string packagePath,
            byte[] manifest,
            IReadOnlyList<(string Path, byte[] Content)> entries)
        {
            string? directory = Path.GetDirectoryName(packagePath);

            if (directory is not null)
            {
                Directory.CreateDirectory(directory);
            }

            using FileStream stream = File.Create(packagePath);
            using ZipArchive archive = new(stream, ZipArchiveMode.Create);
            WriteArchiveEntry(archive, "manifest.json", manifest);

            foreach ((string path, byte[] content) in entries)
            {
                WriteArchiveEntry(archive, path, content);
            }
        }

        private static void WriteArchiveEntry(ZipArchive archive, string path, byte[] content)
        {
            ZipArchiveEntry entry = archive.CreateEntry(path);
            using Stream output = entry.Open();
            output.Write(content);
        }

        private static string GetFixtureAssetsPath()
        {
            return Path.Combine(AppContext.BaseDirectory, "Fixtures", "sharedassets0.assets");
        }

        private static Stream OpenClassPackage()
        {
            return File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures", "resources.tpk"));
        }
    }
}

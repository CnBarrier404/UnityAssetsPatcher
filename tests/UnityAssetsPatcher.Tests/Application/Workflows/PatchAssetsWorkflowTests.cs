using System.Text.Json;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Manifests;
using UnityAssetsPatcher.Application.Workflows;
using UnityAssetsPatcher.Core.Assets;
using UnityAssetsPatcher.Core.Json;
using UnityAssetsPatcher.Tests.Support;
using Xunit;

namespace UnityAssetsPatcher.Tests.Application.Workflows;

public sealed class PatchAssetsWorkflowTests
{
    [Fact]
    public void Preview_WhenManifestLoaderIsInjected_UsesLoaderAndReturnsPatchPreview()
    {
        const string configPath = "missing-manifest.json";
        var manifestLoader = new RecordingManifestLoader(new ModManifest(
            "Injected Manifest",
            "UnityAssetsPatcher.Tests",
            "1.0.0",
            null,
            null,
            [],
            [
                CreateCameraPatch("resources.assets"),
            ]));
        var reader = new StubAssetsFileService(
            [new AssetsInfo(10, 20, "Camera", 128)],
            new Dictionary<long, AssetsFieldInfo>
            {
                [10] = CameraFieldTree("90.0"),
            });
        var workflow = new WorkflowFactory(reader, manifestLoader)
            .CreatePatchAssetsWorkflow(reader);

        PatchPreviewResult preview = workflow.Preview(new PatchPreviewRequest("resources.assets", configPath));

        PatchPreviewAssetResult asset = Assert.Single(preview.Assets);
        Assert.Equal(10, asset.Asset.PathId);
        PatchPreviewOperationResult operation = Assert.Single(asset.Operations);
        Assert.Equal("field of view", operation.Path);
        Assert.True(operation.WillChange);
        Assert.Equal(configPath, manifestLoader.ConfigPath);
    }

    [Fact]
    public void Apply_WhenOperationsCanChange_WritesPatchAndReturnsSummary()
    {
        string inputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.assets");
        string outputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.patched.assets");
        string backupDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        File.WriteAllText(inputPath, "original");
        var manifestLoader = new RecordingManifestLoader(new ModManifest(
            "Injected Manifest",
            "UnityAssetsPatcher.Tests",
            "1.0.0",
            null,
            null,
            [],
            [
                CreateCameraPatch(Path.GetFileName(inputPath)),
            ]));
        var assetsFileService = new StubAssetsFileService(
            [new AssetsInfo(10, 20, "Camera", 128)],
            new Dictionary<long, AssetsFieldInfo>
            {
                [10] = CameraFieldTree("90.0"),
            });
        var workflow = new WorkflowFactory(assetsFileService, manifestLoader)
            .CreatePatchAssetsWorkflow(assetsFileService);

        try
        {
            PatchApplyResult result = workflow.Apply(
                new PatchApplyRequest(inputPath, "missing-manifest.json", outputPath, backupDirectory));

            Assert.Equal(outputPath, result.OutputPath);
            Assert.Null(result.BackupPath);
            Assert.Equal(1, result.AssetCount);
            Assert.Equal(1, result.OperationCount);
            Assert.Equal(inputPath, assetsFileService.InputPath);
            Assert.Equal(outputPath, assetsFileService.OutputPath);
            AssetFieldPatch patch = Assert.Single(assetsFileService.Plan);
            Assert.Equal(10, patch.PathId);
            Assert.Equal("field of view", Assert.Single(patch.Operations).Path);
        }
        finally
        {
            File.Delete(inputPath);
            File.Delete(outputPath);

            if (Directory.Exists(backupDirectory))
            {
                Directory.Delete(backupDirectory, true);
            }
        }
    }

    [Fact]
    public void Apply_ReleasesReadResourcesBeforeWriting()
    {
        string inputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.assets");
        string backupDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        File.WriteAllText(inputPath, "original");
        var manifestLoader = new RecordingManifestLoader(new ModManifest(
            "Injected Manifest",
            "UnityAssetsPatcher.Tests",
            "1.0.0",
            null,
            null,
            [],
            [
                CreateCameraPatch(Path.GetFileName(inputPath)),
            ]));
        var assetsFileService = new StubAssetsFileService(
            [new AssetsInfo(10, 20, "Camera", 128)],
            new Dictionary<long, AssetsFieldInfo>
            {
                [10] = CameraFieldTree("90.0"),
            });
        var workflow = new WorkflowFactory(assetsFileService, manifestLoader)
            .CreatePatchAssetsWorkflow(assetsFileService);

        try
        {
            workflow.Apply(new PatchApplyRequest(inputPath, "missing-manifest.json", null, backupDirectory));

            Assert.True(assetsFileService.DisposeCountAtWrite > 0);
        }
        finally
        {
            File.Delete(inputPath);

            if (Directory.Exists(backupDirectory))
            {
                Directory.Delete(backupDirectory, true);
            }
        }
    }

    private static ManifestPatch CreateCameraPatch(string assetsFileName)
    {
        return new ManifestPatch(
            assetsFileName,
            "Camera",
            [
                new Dictionary<string, JsonElement>
                {
                    ["field of view"] = JsonElementFactory.Number(90.0),
                },
            ],
            [
                new ManifestSetOperation(
                    "field of view",
                    JsonElementFactory.Number(90.0),
                    JsonElementFactory.Number(75.0)),
            ],
            null);
    }

    private static AssetsFieldInfo CameraFieldTree(string fieldOfView)
    {
        return new AssetsFieldInfo(
            "Camera",
            "Camera",
            null,
            [new AssetsFieldInfo("field of view", "float", fieldOfView, [])]);
    }

    private sealed class RecordingManifestLoader : IModManifestLoader
    {
        private readonly ModManifest _manifest;

        public RecordingManifestLoader(ModManifest manifest)
        {
            _manifest = manifest;
        }

        public string? ConfigPath { get; private set; }

        public ModManifest Load(string configPath)
        {
            ConfigPath = configPath;

            return _manifest;
        }
    }
}

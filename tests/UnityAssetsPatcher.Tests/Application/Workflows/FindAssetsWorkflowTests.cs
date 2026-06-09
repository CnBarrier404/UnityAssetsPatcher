using System.Text.Json;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Manifests;
using UnityAssetsPatcher.Application.Patching;
using UnityAssetsPatcher.Application.Workflows;
using UnityAssetsPatcher.Core.Assets;
using UnityAssetsPatcher.Core.Json;
using UnityAssetsPatcher.Tests.Support;
using Xunit;

namespace UnityAssetsPatcher.Tests.Application.Workflows;

public sealed class FindAssetsWorkflowTests
{
    [Fact]
    public void Find_WhenManifestHasMultipleTargets_ReturnsMatchesForRequestedAssetsFileOnly()
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
                CreatePatch("resources.assets", "Main Camera"),
                CreatePatch("sharedassets0.assets", "Ignored Camera"),
            ]));
        var reader = new StubAssetsFileService(
            [new AssetsInfo(10, 20, "Camera", 128)],
            new Dictionary<long, AssetsFieldInfo>
            {
                [10] = new("Camera", "Camera", null, [new AssetsFieldInfo("m_Name", "string", "Main Camera", [])]),
            });
        var workflow = new WorkflowFactory(reader, manifestLoader)
            .CreateFindAssetsWorkflow(reader);

        var matches = workflow.Find(new FindAssetsRequest("resources.assets", configPath));

        AssetMatch match = Assert.Single(matches);
        Assert.Equal(10, match.Asset.PathId);
        Assert.Equal(configPath, manifestLoader.ConfigPath);
    }

    [Fact]
    public void Find_WhenMultiplePatchesUseSameAssetsFile_ReadsAssetsInfoOnce()
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
                CreatePatch("resources.assets", "Main Camera"),
                CreatePatch("resources.assets", "Secondary Camera"),
            ]));
        var reader = new StubAssetsFileService(
            [
                new AssetsInfo(10, 20, "Camera", 128),
                new AssetsInfo(11, 20, "Camera", 128),
            ],
            new Dictionary<long, AssetsFieldInfo>
            {
                [10] = new("Camera", "Camera", null, [new AssetsFieldInfo("m_Name", "string", "Main Camera", [])]),
                [11] = new("Camera", "Camera", null, [new AssetsFieldInfo("m_Name", "string", "Secondary Camera", [])]),
            });
        var workflow = new WorkflowFactory(reader, manifestLoader)
            .CreateFindAssetsWorkflow(reader);

        var matches = workflow.Find(new FindAssetsRequest("resources.assets", configPath));

        Assert.Equal([10L, 11L], matches.Select(match => match.Asset.PathId));
        Assert.Equal(1, reader.ReadAssetsInfoCallCount);
    }

    private static ManifestPatch CreatePatch(string assetsFileName, string cameraName)
    {
        return new ManifestPatch(
            assetsFileName,
            "Camera",
            [
                new Dictionary<string, JsonElement>
                {
                    ["m_Name"] = JsonElementFactory.String(cameraName),
                },
            ],
            null,
            null);
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

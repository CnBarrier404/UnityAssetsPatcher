using UnityAssetsPatcher.Core;
using Xunit;

namespace UnityAssetsPatcher.Tests;

public sealed class AssetsWorkflowServiceTests
{
    /// <summary>
    /// 验证服务层可以直接执行 find 请求，GUI/TUI 不需要构造 CLI 参数。
    /// </summary>
    [Fact]
    public void FindAssets_WhenConfigHasMultipleIncludeGroups_ReturnsAssetsMatchingAnyGroup()
    {
        string configPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        File.WriteAllText(
            configPath,
            """
            {
              "type": "Camera",
              "include": [
                {
                  "field of view": 60.0
                },
                {
                  "field of view": 90.0
                }
              ]
            }
            """);
        var service = new AssetsWorkflowService(new StubAssetsReader(
            [
                new AssetsInfo(1, 20, "Camera", 128),
                new AssetsInfo(2, 20, "Camera", 128),
                new AssetsInfo(3, 20, "Camera", 128),
            ],
            new Dictionary<long, AssetsFieldInfo>
            {
                [1] = new("Camera", "Camera", null, [new AssetsFieldInfo("field of view", "float", "60.0", [])]),
                [2] = new("Camera", "Camera", null, [new AssetsFieldInfo("field of view", "float", "90.0", [])]),
                [3] = new("Camera", "Camera", null, [new AssetsFieldInfo("field of view", "float", "75.0", [])]),
            }));

        try
        {
            var matches =
                service.FindAssets(new FindAssetsRequest("resources.assets", configPath));

            Assert.Equal([1L, 2L], matches.Select(match => match.Asset.PathId));
        }
        finally
        {
            File.Delete(configPath);
        }
    }

    /// <summary>
    /// 验证服务层 patch preview 返回结构化的 planned/skipped 结果。
    /// </summary>
    [Fact]
    public void PreviewPatch_WhenSetFromDoesNotMatch_ReturnsSkippedOperationResult()
    {
        string configPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        File.WriteAllText(
            configPath,
            """
            {
              "type": "Camera",
              "include": [
                {
                  "field of view": 90.0
                }
              ],
              "set": [
                {
                  "path": "field of view",
                  "from": 60.0,
                  "to": 75.0
                }
              ]
            }
            """);
        var service = new AssetsWorkflowService(new StubAssetsReader(
            [new AssetsInfo(4, 20, "Camera", 128)],
            new Dictionary<long, AssetsFieldInfo>
            {
                [4] = new("Camera", "Camera", null, [new AssetsFieldInfo("field of view", "float", "90.0", [])]),
            }));

        try
        {
            PatchPreviewResult preview = service.PreviewPatch(new PatchPreviewRequest("resources.assets", configPath));

            PatchPreviewOperationResult operation = Assert.Single(Assert.Single(preview.Assets).Operations);
            Assert.False(operation.WillChange);
            Assert.Equal("field of view", operation.Path);
            Assert.Equal("90.0", operation.OldValue);
        }
        finally
        {
            File.Delete(configPath);
        }
    }

    private sealed class StubAssetsReader : IAssetsReader
    {
        private readonly IReadOnlyList<AssetsInfo> _result;
        private readonly IReadOnlyDictionary<long, AssetsFieldInfo> _fieldTrees;

        public StubAssetsReader(IReadOnlyList<AssetsInfo> result, IReadOnlyDictionary<long, AssetsFieldInfo> fieldTrees)
        {
            _result = result;
            _fieldTrees = fieldTrees;
        }

        public IReadOnlyList<AssetsInfo> ReadAssetsInfo(string assetsFilePath)
        {
            return _result;
        }

        public AssetsFieldInfo ReadAssetsFieldInfo(string assetsFilePath, long pathId)
        {
            return _fieldTrees.TryGetValue(pathId, out AssetsFieldInfo? fieldTree)
                ? fieldTree
                : throw new InvalidOperationException("Field tree was not configured.");
        }
    }
}

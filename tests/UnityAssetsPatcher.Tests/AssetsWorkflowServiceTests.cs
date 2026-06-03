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

    /// <summary>
    /// 验证 apply 在所有 set 操作可应用时，会调用写入器并返回输出摘要。
    /// </summary>
    [Fact]
    public void ApplyPatch_WhenAllOperationsCanChange_CallsWriterAndReturnsSummary()
    {
        string configPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        string inputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.assets");
        string outputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.patched.assets");
        string backupDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        File.WriteAllText(inputPath, "original");
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
                  "from": 90.0,
                  "to": 75.0
                }
              ]
            }
            """);
        var writer = new StubAssetsPatchWriter();
        var service = new AssetsWorkflowService(
            new StubAssetsReader(
                [new AssetsInfo(4, 20, "Camera", 128)],
                new Dictionary<long, AssetsFieldInfo>
                {
                    [4] = new("Camera", "Camera", null, [new AssetsFieldInfo("field of view", "float", "90.0", [])]),
                }),
            writer);

        try
        {
            PatchApplyResult result =
                service.ApplyPatch(new PatchApplyRequest(inputPath, configPath, outputPath, backupDirectory));

            Assert.Equal(outputPath, result.OutputPath);
            Assert.Null(result.BackupPath);
            Assert.Equal(1, result.AssetCount);
            Assert.Equal(1, result.OperationCount);
            Assert.Equal(inputPath, writer.InputPath);
            Assert.Equal(outputPath, writer.OutputPath);
            PatchWriteAsset asset = Assert.Single(writer.Plan);
            Assert.Equal(4, asset.PathId);
            Assert.Equal("field of view", Assert.Single(asset.Operations).Path);
        }
        finally
        {
            File.Delete(configPath);
            File.Delete(inputPath);
            File.Delete(outputPath);
            if (Directory.Exists(backupDirectory))
            {
                Directory.Delete(backupDirectory, true);
            }
        }
    }

    /// <summary>
    /// 验证 apply 在 from 不匹配时严格失败，并且不会写文件。
    /// </summary>
    [Fact]
    public void ApplyPatch_WhenSetFromDoesNotMatch_ThrowsWithoutCallingWriter()
    {
        string configPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        string inputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.assets");
        string outputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.patched.assets");
        File.WriteAllText(inputPath, "original");
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
        var writer = new StubAssetsPatchWriter();
        var service = new AssetsWorkflowService(
            new StubAssetsReader(
                [new AssetsInfo(4, 20, "Camera", 128)],
                new Dictionary<long, AssetsFieldInfo>
                {
                    [4] = new("Camera", "Camera", null, [new AssetsFieldInfo("field of view", "float", "90.0", [])]),
                }),
            writer);

        try
        {
            var exception = Assert.Throws<InvalidOperationException>(() =>
                service.ApplyPatch(new PatchApplyRequest(inputPath, configPath, outputPath, Path.GetTempPath())));

            Assert.Contains("cannot be applied", exception.Message);
            Assert.False(writer.WasCalled);
        }
        finally
        {
            File.Delete(configPath);
            File.Delete(inputPath);
        }
    }

    /// <summary>
    /// 验证 apply 的 --output 不会覆盖已存在文件。
    /// </summary>
    [Fact]
    public void ApplyPatch_WhenOutputPathExists_ThrowsWithoutCallingWriter()
    {
        string configPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        string inputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.assets");
        string outputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.patched.assets");
        File.WriteAllText(inputPath, "original");
        File.WriteAllText(outputPath, "existing");
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
                  "from": 90.0,
                  "to": 75.0
                }
              ]
            }
            """);
        var writer = new StubAssetsPatchWriter();
        var service = new AssetsWorkflowService(
            new StubAssetsReader(
                [new AssetsInfo(4, 20, "Camera", 128)],
                new Dictionary<long, AssetsFieldInfo>
                {
                    [4] = new("Camera", "Camera", null, [new AssetsFieldInfo("field of view", "float", "90.0", [])]),
                }),
            writer);

        try
        {
            var exception = Assert.Throws<IOException>(() =>
                service.ApplyPatch(new PatchApplyRequest(inputPath, configPath, outputPath, Path.GetTempPath())));

            Assert.Contains("already exists", exception.Message);
            Assert.False(writer.WasCalled);
        }
        finally
        {
            File.Delete(configPath);
            File.Delete(inputPath);
            File.Delete(outputPath);
        }
    }

    /// <summary>
    /// 验证 apply 未指定输出路径时，会先创建备份再覆盖输入文件。
    /// </summary>
    [Fact]
    public void ApplyPatch_WhenOutputIsOmitted_CreatesTimestampedBackupAndOverwritesInput()
    {
        string configPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        string inputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.assets");
        string backupDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        File.WriteAllText(inputPath, "original");
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
                  "from": 90.0,
                  "to": 75.0
                }
              ]
            }
            """);
        var writer = new StubAssetsPatchWriter();
        var service = new AssetsWorkflowService(
            new StubAssetsReader(
                [new AssetsInfo(4, 20, "Camera", 128)],
                new Dictionary<long, AssetsFieldInfo>
                {
                    [4] = new("Camera", "Camera", null, [new AssetsFieldInfo("field of view", "float", "90.0", [])]),
                }),
            writer);

        try
        {
            PatchApplyResult result = service.ApplyPatch(new PatchApplyRequest(
                inputPath,
                configPath,
                null,
                backupDirectory));

            Assert.Equal(inputPath, result.OutputPath);
            Assert.NotNull(result.BackupPath);
            Assert.StartsWith(backupDirectory, result.BackupPath, StringComparison.OrdinalIgnoreCase);
            Assert.EndsWith(".assets", result.BackupPath);
            Assert.True(File.Exists(result.BackupPath));
            Assert.Equal("original", File.ReadAllText(result.BackupPath));
            Assert.Equal("patched", File.ReadAllText(inputPath));
        }
        finally
        {
            File.Delete(configPath);
            File.Delete(inputPath);
            if (Directory.Exists(backupDirectory))
            {
                Directory.Delete(backupDirectory, true);
            }
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

    private sealed class StubAssetsPatchWriter : IAssetsPatchWriter
    {
        public bool WasCalled { get; private set; }
        public string? InputPath { get; private set; }
        public string? OutputPath { get; private set; }
        public IReadOnlyList<PatchWriteAsset> Plan { get; private set; } = [];

        public void WritePatch(string inputPath, string outputPath, IReadOnlyList<PatchWriteAsset> plan)
        {
            WasCalled = true;
            InputPath = inputPath;
            OutputPath = outputPath;
            Plan = plan;
            File.WriteAllText(outputPath, "patched");
        }
    }
}

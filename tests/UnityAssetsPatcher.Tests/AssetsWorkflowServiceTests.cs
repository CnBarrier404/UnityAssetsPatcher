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
    /// 验证一个 patch 配置可以同时预览多个组件类型的修改。
    /// </summary>
    [Fact]
    public void PreviewPatch_WhenConfigHasMultiplePatchTargets_ReturnsOperationsForEachTarget()
    {
        string configPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        File.WriteAllText(
            configPath,
            """
            {
              "patches": [
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
                },
                {
                  "type": "Light",
                  "include": [
                    {
                      "m_Intensity": 1.0
                    }
                  ],
                  "set": [
                    {
                      "path": "m_Intensity",
                      "from": 1.0,
                      "to": 2.0
                    }
                  ]
                }
              ]
            }
            """);
        var service = new AssetsWorkflowService(new StubAssetsReader(
            [
                new AssetsInfo(4, 20, "Camera", 128),
                new AssetsInfo(5, 108, "Light", 96),
            ],
            new Dictionary<long, AssetsFieldInfo>
            {
                [4] = new("Camera", "Camera", null, [new AssetsFieldInfo("field of view", "float", "90.0", [])]),
                [5] = new("Light", "Light", null, [new AssetsFieldInfo("m_Intensity", "float", "1.0", [])]),
            }));

        try
        {
            PatchPreviewResult preview = service.PreviewPatch(new PatchPreviewRequest("resources.assets", configPath));

            Assert.Equal([4L, 5L], preview.Assets.Select(asset => asset.Asset.PathId));
            Assert.Equal("field of view", Assert.Single(preview.Assets[0].Operations).Path);
            Assert.Equal("m_Intensity", Assert.Single(preview.Assets[1].Operations).Path);
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
    /// 验证 apply 可以把多个组件类型的修改合并成一次写入计划。
    /// </summary>
    [Fact]
    public void ApplyPatch_WhenConfigHasMultiplePatchTargets_WritesAllMatchedAssets()
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
              "patches": [
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
                },
                {
                  "type": "Light",
                  "include": [
                    {
                      "m_Intensity": 1.0
                    }
                  ],
                  "set": [
                    {
                      "path": "m_Intensity",
                      "from": 1.0,
                      "to": 2.0
                    }
                  ]
                }
              ]
            }
            """);
        var writer = new StubAssetsPatchWriter();
        var service = new AssetsWorkflowService(
            new StubAssetsReader(
                [
                    new AssetsInfo(4, 20, "Camera", 128),
                    new AssetsInfo(5, 108, "Light", 96),
                ],
                new Dictionary<long, AssetsFieldInfo>
                {
                    [4] = new("Camera", "Camera", null, [new AssetsFieldInfo("field of view", "float", "90.0", [])]),
                    [5] = new("Light", "Light", null, [new AssetsFieldInfo("m_Intensity", "float", "1.0", [])]),
                }),
            writer);

        try
        {
            PatchApplyResult result =
                service.ApplyPatch(new PatchApplyRequest(inputPath, configPath, outputPath, backupDirectory));

            Assert.Equal(2, result.AssetCount);
            Assert.Equal(2, result.OperationCount);
            Assert.Equal([4L, 5L], writer.Plan.Select(asset => asset.PathId));
            Assert.Equal("field of view", Assert.Single(writer.Plan[0].Operations).Path);
            Assert.Equal("m_Intensity", Assert.Single(writer.Plan[1].Operations).Path);
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
    /// 验证多个 patch target 命中同一个资产时，会合并为同一个写入资产项，避免后一次写入覆盖前一次修改。
    /// </summary>
    [Fact]
    public void ApplyPatch_WhenMultiplePatchTargetsMatchSameAsset_MergesOperationsForSingleWriteAsset()
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
              "patches": [
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
                },
                {
                  "type": "Camera",
                  "include": [
                    {
                      "near clip plane": 0.3
                    }
                  ],
                  "set": [
                    {
                      "path": "near clip plane",
                      "from": 0.3,
                      "to": 0.1
                    }
                  ]
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
                    [4] = new("Camera", "Camera", null,
                    [
                        new AssetsFieldInfo("field of view", "float", "90.0", []),
                        new AssetsFieldInfo("near clip plane", "float", "0.3", []),
                    ]),
                }),
            writer);

        try
        {
            PatchApplyResult result =
                service.ApplyPatch(new PatchApplyRequest(inputPath, configPath, outputPath, backupDirectory));

            Assert.Equal(1, result.AssetCount);
            Assert.Equal(2, result.OperationCount);
            PatchWriteAsset asset = Assert.Single(writer.Plan);
            Assert.Equal(4, asset.PathId);
            Assert.Equal(["field of view", "near clip plane"], asset.Operations.Select(operation => operation.Path));
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
    /// 验证 patch 可以用 UABEA 风格的单元素对象数组描述复合字段，并在写入计划中展开为子字段修改。
    /// </summary>
    [Fact]
    public void ApplyPatch_WhenSetUsesCompositeObjectArray_ExpandsChildFieldOperations()
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
              "type": "Light",
              "include": [
                {
                  "m_Color": [
                    {
                      "r": 0.5411765,
                      "g": 0.61960787,
                      "b": 0.67058825,
                      "a": 1.0
                    }
                  ]
                }
              ],
              "set": [
                {
                  "path": "m_Color",
                  "from": [
                    {
                      "r": 0.5411765,
                      "g": 0.61960787,
                      "b": 0.67058825,
                      "a": 1.0
                    }
                  ],
                  "to": [
                    {
                      "r": 1.0,
                      "g": 1.0,
                      "b": 1.0,
                      "a": 1.0
                    }
                  ]
                }
              ]
            }
            """);
        var writer = new StubAssetsPatchWriter();
        var service = new AssetsWorkflowService(
            new StubAssetsReader(
                [new AssetsInfo(8, 108, "Light", 96)],
                new Dictionary<long, AssetsFieldInfo>
                {
                    [8] = new("Light", "Light", null,
                    [
                        new AssetsFieldInfo("m_Color", "ColorRGBA", null,
                        [
                            new AssetsFieldInfo("r", "float", "0.5411765", []),
                            new AssetsFieldInfo("g", "float", "0.61960787", []),
                            new AssetsFieldInfo("b", "float", "0.67058825", []),
                            new AssetsFieldInfo("a", "float", "1.0", []),
                        ]),
                    ]),
                }),
            writer);

        try
        {
            PatchApplyResult result =
                service.ApplyPatch(new PatchApplyRequest(inputPath, configPath, outputPath, backupDirectory));

            Assert.Equal(1, result.AssetCount);
            Assert.Equal(4, result.OperationCount);
            PatchWriteAsset asset = Assert.Single(writer.Plan);
            Assert.Equal(
                ["m_Color.r", "m_Color.g", "m_Color.b", "m_Color.a"],
                asset.Operations.Select(operation => operation.Path));
            Assert.All(asset.Operations, operation => Assert.Equal(1.0d, operation.To.GetDouble()));
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

using UnityAssetsPatcher.Core;
using Xunit;

namespace UnityAssetsPatcher.Tests;

public sealed class AssetsWorkflowServiceTests
{
    /// <summary>
    /// Verifies that the service layer can execute find requests directly without GUI/TUI code building CLI arguments.
    /// </summary>
    [Fact]
    public void FindAssets_WhenConfigHasMultipleIncludeGroups_ReturnsAssetsMatchingAnyGroup()
    {
        string configPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        TestManifest.Write(
            configPath,
            """
            {
              "target": "resources.assets",
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
    /// Verifies that service-layer patch preview returns structured planned/skipped results.
    /// </summary>
    [Fact]
    public void PreviewPatch_WhenSetFromDoesNotMatch_ReturnsSkippedOperationResult()
    {
        string configPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        TestManifest.Write(
            configPath,
            """
            {
              "target": "resources.assets",
              "type": "Camera",
              "include": [
                {
                  "field of view": 90.0
                }
              ],
              "set": [
                {
                  "field": "field of view",
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
    /// Verifies that one patch config can preview changes for multiple component types.
    /// </summary>
    [Fact]
    public void PreviewPatch_WhenConfigHasMultiplePatchTargets_ReturnsOperationsForEachTarget()
    {
        string configPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        TestManifest.Write(
            configPath,
            """
            {
              "patches": [
                {
                  "target": "resources.assets",
                  "type": "Camera",
                  "include": [
                    {
                      "field of view": 90.0
                    }
                  ],
                  "set": [
                    {
                      "field": "field of view",
                      "from": 90.0,
                      "to": 75.0
                    }
                  ]
                },
                {
                  "target": "resources.assets",
                  "type": "Light",
                  "include": [
                    {
                      "m_Intensity": 1.0
                    }
                  ],
                  "set": [
                    {
                      "field": "m_Intensity",
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
    /// Verifies that apply calls the writer and returns an output summary when all set operations can change.
    /// </summary>
    [Fact]
    public void ApplyPatch_WhenAllOperationsCanChange_CallsWriterAndReturnsSummary()
    {
        string configPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        string inputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.assets");
        string outputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.patched.assets");
        string backupDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        File.WriteAllText(inputPath, "original");
        TestManifest.Write(
            configPath,
            $$"""
              {
                "target": "{{Path.GetFileName(inputPath)}}",
                "type": "Camera",
                "include": [
                  {
                    "field of view": 90.0
                  }
                ],
                "set": [
                  {
                    "field": "field of view",
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
    /// Verifies that apply can merge changes for multiple component types into one write plan.
    /// </summary>
    [Fact]
    public void ApplyPatch_WhenConfigHasMultiplePatchTargets_WritesAllMatchedAssets()
    {
        string configPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        string inputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.assets");
        string outputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.patched.assets");
        string backupDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        File.WriteAllText(inputPath, "original");
        TestManifest.Write(
            configPath,
            $$"""
              {
                "patches": [
                  {
                    "target": "{{Path.GetFileName(inputPath)}}",
                    "type": "Camera",
                    "include": [
                      {
                        "field of view": 90.0
                      }
                    ],
                    "set": [
                      {
                        "field": "field of view",
                        "from": 90.0,
                        "to": 75.0
                      }
                    ]
                  },
                  {
                    "target": "{{Path.GetFileName(inputPath)}}",
                    "type": "Light",
                    "include": [
                      {
                        "m_Intensity": 1.0
                      }
                    ],
                    "set": [
                      {
                        "field": "m_Intensity",
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
    /// Verifies that multiple patch targets matching one asset merge into one write asset, avoiding overwrite loss.
    /// </summary>
    [Fact]
    public void ApplyPatch_WhenMultiplePatchTargetsMatchSameAsset_MergesOperationsForSingleWriteAsset()
    {
        string configPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        string inputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.assets");
        string outputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.patched.assets");
        string backupDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        File.WriteAllText(inputPath, "original");
        TestManifest.Write(
            configPath,
            $$"""
              {
                "patches": [
                  {
                    "target": "{{Path.GetFileName(inputPath)}}",
                    "type": "Camera",
                    "include": [
                      {
                        "field of view": 90.0
                      }
                    ],
                    "set": [
                      {
                        "field": "field of view",
                        "from": 90.0,
                        "to": 75.0
                      }
                    ]
                  },
                  {
                    "target": "{{Path.GetFileName(inputPath)}}",
                    "type": "Camera",
                    "include": [
                      {
                        "near clip plane": 0.3
                      }
                    ],
                    "set": [
                      {
                        "field": "near clip plane",
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
    /// Verifies that patch can describe composite fields with UABEA-style single-object arrays and expand child writes.
    /// </summary>
    [Fact]
    public void ApplyPatch_WhenSetUsesCompositeObjectArray_ExpandsChildFieldOperations()
    {
        string configPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        string inputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.assets");
        string outputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.patched.assets");
        string backupDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        File.WriteAllText(inputPath, "original");
        TestManifest.Write(
            configPath,
            $$"""
              {
                "target": "{{Path.GetFileName(inputPath)}}",
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
                    "field": "m_Color",
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
    /// Verifies that apply can resolve a Path ID by Texture2D name and write it to a Material texture slot m_PathID.
    /// </summary>
    [Fact]
    public void ApplyPatch_WhenSetToUsesPathIdResolver_WritesResolvedTexturePathId()
    {
        string configPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        string inputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.assets");
        string outputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.patched.assets");
        string backupDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        File.WriteAllText(inputPath, "original");
        TestManifest.Write(
            configPath,
            $$"""
              {
                "target": "{{Path.GetFileName(inputPath)}}",
                "type": "Material",
                "include": [
                  {
                    "m_Name": "TargetMaterial"
                  }
                ],
                "set": [
                  {
                    "field": "m_SavedProperties.m_TexEnvs.Array.data[first=_EmissionMap].second.m_Texture.m_PathID",
                    "from": 0,
                    "to": {
                      "$pathId": {
                        "type": "Texture2D",
                        "include": [
                          {
                            "m_Name": "EmissionTexture"
                          }
                        ]
                      }
                    }
                  }
                ]
              }
              """);
        var writer = new StubAssetsPatchWriter();
        var service = new AssetsWorkflowService(
            new StubAssetsReader(
                [
                    new AssetsInfo(4, 21, "Material", 256),
                    new AssetsInfo(8842, 28, "Texture2D", 512),
                ],
                new Dictionary<long, AssetsFieldInfo>
                {
                    [4] = CreateMaterialFieldTree(pathId: "0"),
                    [8842] = new("Texture2D", "Texture2D", null,
                    [
                        new AssetsFieldInfo("m_Name", "string", "EmissionTexture", []),
                    ]),
                }),
            writer);

        try
        {
            PatchApplyResult result =
                service.ApplyPatch(new PatchApplyRequest(inputPath, configPath, outputPath, backupDirectory));

            Assert.Equal(1, result.AssetCount);
            Assert.Equal(1, result.OperationCount);
            PatchWriteOperation operation = Assert.Single(Assert.Single(writer.Plan).Operations);
            Assert.Equal(
                "m_SavedProperties.m_TexEnvs.Array.data[first=_EmissionMap].second.m_Texture.m_PathID",
                operation.Path);
            Assert.Equal("0", operation.OldValue);
            Assert.Equal(8842, operation.To.GetInt64());
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
    /// Verifies that apply fails strictly when from does not match and does not write files.
    /// </summary>
    [Fact]
    public void ApplyPatch_WhenSetFromDoesNotMatch_ThrowsWithoutCallingWriter()
    {
        string configPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        string inputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.assets");
        string outputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.patched.assets");
        File.WriteAllText(inputPath, "original");
        TestManifest.Write(
            configPath,
            $$"""
              {
                "target": "{{Path.GetFileName(inputPath)}}",
                "type": "Camera",
                "include": [
                  {
                    "field of view": 90.0
                  }
                ],
                "set": [
                  {
                    "field": "field of view",
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
    /// Verifies that apply --output does not overwrite an existing file.
    /// </summary>
    [Fact]
    public void ApplyPatch_WhenOutputPathExists_ThrowsWithoutCallingWriter()
    {
        string configPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        string inputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.assets");
        string outputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.patched.assets");
        File.WriteAllText(inputPath, "original");
        File.WriteAllText(outputPath, "existing");
        TestManifest.Write(
            configPath,
            $$"""
              {
                "target": "{{Path.GetFileName(inputPath)}}",
                "type": "Camera",
                "include": [
                  {
                    "field of view": 90.0
                  }
                ],
                "set": [
                  {
                    "field": "field of view",
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
    /// Verifies that apply creates a backup before overwriting the input file when no output path is specified.
    /// </summary>
    [Fact]
    public void ApplyPatch_WhenOutputIsOmitted_CreatesTimestampedBackupAndOverwritesInput()
    {
        string configPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        string inputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.assets");
        string backupDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        File.WriteAllText(inputPath, "original");
        TestManifest.Write(
            configPath,
            $$"""
              {
                "target": "{{Path.GetFileName(inputPath)}}",
                "type": "Camera",
                "include": [
                  {
                    "field of view": 90.0
                  }
                ],
                "set": [
                  {
                    "field": "field of view",
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

    /// <summary>
    /// Verifies that explicit patch uses only patch targets whose target matches the input file name.
    /// </summary>
    [Fact]
    public void PreviewPatch_WhenConfigContainsDifferentTargets_UsesOnlySelectedAssetsFileTarget()
    {
        string configPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        TestManifest.Write(
            configPath,
            """
            {
              "patches": [
                {
                  "target": "resources.assets",
                  "type": "Camera",
                  "include": [
                    {
                      "field of view": 90.0
                    }
                  ],
                  "set": [
                    {
                      "field": "field of view",
                      "from": 90.0,
                      "to": 75.0
                    }
                  ]
                },
                {
                  "target": "sharedassets1.assets",
                  "type": "Light",
                  "include": [
                    {
                      "m_Intensity": 1.0
                    }
                  ],
                  "set": [
                    {
                      "field": "m_Intensity",
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

            PatchPreviewAssetResult asset = Assert.Single(preview.Assets);
            Assert.Equal(4, asset.Asset.PathId);
            Assert.Equal("field of view", Assert.Single(asset.Operations).Path);
        }
        finally
        {
            File.Delete(configPath);
        }
    }

    /// <summary>
    /// Verifies that install locates assets files from zip manifest targets under the game directory and writes in place.
    /// </summary>
    [Fact]
    public void InstallMod_WhenZipTargetMatchesSingleFile_OverwritesTargetAndReturnsSummary()
    {
        string zipPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.zip");
        string gameDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string targetDirectory = Path.Combine(gameDirectory, "Game_Data");
        string targetPath = Path.Combine(targetDirectory, "sharedassets0.assets");
        string backupDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(targetDirectory);
        File.WriteAllText(targetPath, "original");
        TestManifest.WriteZip(
            zipPath,
            """
            {
              "patches": [
                {
                  "target": "sharedassets0.assets",
                  "type": "Camera",
                  "include": [
                    {
                      "field of view": 90.0
                    }
                  ],
                  "set": [
                    {
                      "field": "m_CullingMask.m_Bits",
                      "from": 3211820983,
                      "to": 931037111
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
                        new AssetsFieldInfo("m_CullingMask", "BitField", null,
                        [
                            new AssetsFieldInfo("m_Bits", "UInt32", "3211820983", []),
                        ]),
                    ]),
                }),
            writer);

        try
        {
            InstallModResult result = service.InstallMod(
                new InstallModRequest(zipPath, gameDirectory, backupDirectory));

            Assert.Equal("Test Mod", result.ModName);
            InstallModFileResult file = Assert.Single(result.Files);
            Assert.Equal("sharedassets0.assets", file.Target);
            Assert.Equal(targetPath, file.AssetsFilePath);
            Assert.StartsWith(backupDirectory, file.BackupPath, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(1, file.AssetCount);
            Assert.Equal(1, file.OperationCount);
            Assert.Equal(targetPath, writer.InputPath);
            Assert.Equal(targetPath, writer.OutputPath);
            Assert.Equal("patched", File.ReadAllText(targetPath));
            Assert.True(File.Exists(file.BackupPath));
        }
        finally
        {
            File.Delete(zipPath);
            if (Directory.Exists(gameDirectory))
            {
                Directory.Delete(gameDirectory, true);
            }

            if (Directory.Exists(backupDirectory))
            {
                Directory.Delete(backupDirectory, true);
            }
        }
    }

    /// <summary>
    /// Verifies that install preview locates assets files from zip manifest targets without requiring a writer.
    /// </summary>
    [Fact]
    public void PreviewInstallMod_WhenZipTargetMatchesSingleFile_ReturnsDryRunResultsWithoutWriter()
    {
        string zipPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.zip");
        string gameDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string targetDirectory = Path.Combine(gameDirectory, "Game_Data");
        string targetPath = Path.Combine(targetDirectory, "sharedassets0.assets");
        Directory.CreateDirectory(targetDirectory);
        File.WriteAllText(targetPath, "original");
        TestManifest.WriteZip(
            zipPath,
            """
            {
              "patches": [
                {
                  "target": "sharedassets0.assets",
                  "type": "Camera",
                  "include": [
                    {
                      "field of view": 90.0
                    }
                  ],
                  "set": [
                    {
                      "field": "m_CullingMask.m_Bits",
                      "from": 3211820983,
                      "to": 931037111
                    }
                  ]
                }
              ]
            }
            """);
        var service = new AssetsWorkflowService(new StubAssetsReader(
            [new AssetsInfo(4, 20, "Camera", 128)],
            new Dictionary<long, AssetsFieldInfo>
            {
                [4] = new("Camera", "Camera", null,
                [
                    new AssetsFieldInfo("field of view", "float", "90.0", []),
                    new AssetsFieldInfo("m_CullingMask", "BitField", null,
                    [
                        new AssetsFieldInfo("m_Bits", "UInt32", "3211820983", []),
                    ]),
                ]),
            }));

        try
        {
            InstallPreviewResult result = service.PreviewInstallMod(
                new InstallPreviewRequest(zipPath, gameDirectory));

            Assert.Equal("Test Mod", result.ModName);
            InstallPreviewFileResult file = Assert.Single(result.Files);
            Assert.Equal("sharedassets0.assets", file.Target);
            Assert.Equal(targetPath, file.AssetsFilePath);
            PatchPreviewAssetResult asset = Assert.Single(file.Preview.Assets);
            Assert.Equal(4, asset.Asset.PathId);
            PatchPreviewOperationResult operation = Assert.Single(asset.Operations);
            Assert.True(operation.WillChange);
            Assert.Equal("m_CullingMask.m_Bits", operation.Path);
            Assert.Equal("3211820983", operation.OldValue);
            Assert.Equal("original", File.ReadAllText(targetPath));
        }
        finally
        {
            File.Delete(zipPath);
            if (Directory.Exists(gameDirectory))
            {
                Directory.Delete(gameDirectory, true);
            }
        }
    }

    /// <summary>
    /// Verifies that no write occurs when a target file name matches multiple files under the game directory.
    /// </summary>
    [Fact]
    public void InstallMod_WhenTargetMatchesMultipleFiles_ThrowsWithoutWriting()
    {
        string zipPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.zip");
        string gameDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string firstDirectory = Path.Combine(gameDirectory, "Game_Data");
        string secondDirectory = Path.Combine(gameDirectory, "Backup_Data");
        string backupDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(firstDirectory);
        Directory.CreateDirectory(secondDirectory);
        File.WriteAllText(Path.Combine(firstDirectory, "sharedassets0.assets"), "original");
        File.WriteAllText(Path.Combine(secondDirectory, "sharedassets0.assets"), "original");
        TestManifest.WriteZip(
            zipPath,
            """
            {
              "patches": [
                {
                  "target": "sharedassets0.assets",
                  "type": "Camera",
                  "include": [
                    {
                      "field of view": 90.0
                    }
                  ],
                  "set": [
                    {
                      "field": "field of view",
                      "from": 90.0,
                      "to": 75.0
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
                    [4] = new("Camera", "Camera", null, [new AssetsFieldInfo("field of view", "float", "90.0", [])]),
                }),
            writer);

        try
        {
            var exception = Assert.Throws<InvalidOperationException>(() =>
                service.InstallMod(new InstallModRequest(zipPath, gameDirectory, backupDirectory)));

            Assert.Contains("matched multiple files", exception.Message);
            Assert.False(writer.WasCalled);
        }
        finally
        {
            File.Delete(zipPath);
            if (Directory.Exists(gameDirectory))
            {
                Directory.Delete(gameDirectory, true);
            }

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

    private static AssetsFieldInfo CreateMaterialFieldTree(string pathId)
    {
        return new AssetsFieldInfo(
            "Material",
            "Material",
            null,
            [
                new AssetsFieldInfo("m_Name", "string", "TargetMaterial", []),
                new AssetsFieldInfo("m_SavedProperties", "UnityPropertySheet", null,
                [
                    new AssetsFieldInfo("m_TexEnvs", "map", null,
                    [
                        new AssetsFieldInfo("Array", "Array", null,
                        [
                            CreateTexEnv("_MainTex", "17"),
                            CreateTexEnv("_EmissionMap", pathId),
                        ]),
                    ]),
                ]),
            ]);
    }

    private static AssetsFieldInfo CreateTexEnv(string name, string pathId)
    {
        return new AssetsFieldInfo(
            "data",
            "pair",
            null,
            [
                new AssetsFieldInfo("first", "string", name, []),
                new AssetsFieldInfo("second", "UnityTexEnv", null,
                [
                    new AssetsFieldInfo("m_Texture", "PPtr<Texture2D>", null,
                    [
                        new AssetsFieldInfo("m_FileID", "int", "0", []),
                        new AssetsFieldInfo("m_PathID", "SInt64", pathId, []),
                    ]),
                ]),
            ]);
    }
}

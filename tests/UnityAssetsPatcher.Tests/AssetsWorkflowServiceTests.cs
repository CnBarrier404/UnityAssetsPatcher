using System.Text.Json;
using System.IO.Compression;
using Xunit;
using UnityAssetsPatcher.Application.Manifests;
using UnityAssetsPatcher.Application.Workflows;
using UnityAssetsPatcher.Core.Assets;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Installing;
using UnityAssetsPatcher.Core.Json;

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
        var service = new AssetsWorkflowService(new StubAssetsFileService(
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
    /// Verifies that workflows can receive a parsed manifest from an injected loader instead of reading the config path directly.
    /// </summary>
    [Fact]
    public void FindAssets_WhenManifestLoaderIsInjected_UsesLoaderInsteadOfReadingConfigPath()
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
                new ManifestPatch(
                    "resources.assets",
                    "Camera",
                    [
                        new Dictionary<string, JsonElement>
                        {
                            ["m_Name"] = JsonElementFactory.String("Main Camera"),
                        },
                    ],
                    null,
                    null),
            ]));
        var assetsFileService = new StubAssetsFileService(
            [new AssetsInfo(10, 20, "Camera", 128)],
            new Dictionary<long, AssetsFieldInfo>
            {
                [10] = new("Camera", "Camera", null, [new AssetsFieldInfo("m_Name", "string", "Main Camera", [])]),
            });
        var service = new AssetsWorkflowService(assetsFileService, assetsFileService, manifestLoader);

        var matches = service.FindAssets(new FindAssetsRequest("resources.assets", configPath));

        AssetMatch match = Assert.Single(matches);
        Assert.Equal(10, match.Asset.PathId);
        Assert.Equal(configPath, manifestLoader.ConfigPath);
    }

    [Fact]
    public void FindAssets_WhenMultipleTargetsUseSameAssetsFile_ReadsAssetsInfoOnce()
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
                new ManifestPatch(
                    "resources.assets",
                    "Camera",
                    [
                        new Dictionary<string, JsonElement>
                        {
                            ["m_Name"] = JsonElementFactory.String("Main Camera"),
                        },
                    ],
                    null,
                    null),
                new ManifestPatch(
                    "resources.assets",
                    "Camera",
                    [
                        new Dictionary<string, JsonElement>
                        {
                            ["m_Name"] = JsonElementFactory.String("Secondary Camera"),
                        },
                    ],
                    null,
                    null),
            ]));
        var assetsFileService = new StubAssetsFileService(
            [
                new AssetsInfo(10, 20, "Camera", 128),
                new AssetsInfo(11, 20, "Camera", 128),
                new AssetsInfo(12, 1, "GameObject", 128),
            ],
            new Dictionary<long, AssetsFieldInfo>
            {
                [10] = new("Camera", "Camera", null, [new AssetsFieldInfo("m_Name", "string", "Main Camera", [])]),
                [11] = new("Camera", "Camera", null, [new AssetsFieldInfo("m_Name", "string", "Secondary Camera", [])]),
            });
        var service = new AssetsWorkflowService(assetsFileService, assetsFileService, manifestLoader);

        var matches = service.FindAssets(new FindAssetsRequest("resources.assets", configPath));

        Assert.Equal([10L, 11L], matches.Select(match => match.Asset.PathId));
        Assert.Equal(1, assetsFileService.ReadAssetsInfoCallCount);
    }

    /// <summary>
    /// Verifies that patch preview can receive a parsed manifest from an injected loader instead of reading the config path directly.
    /// </summary>
    [Fact]
    public void PreviewPatch_WhenManifestLoaderIsInjected_UsesLoaderInsteadOfReadingConfigPath()
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
                new ManifestPatch(
                    "resources.assets",
                    "Camera",
                    [
                        new Dictionary<string, JsonElement>
                        {
                            ["m_Name"] = JsonElementFactory.String("Main Camera"),
                        },
                    ],
                    [
                        new ManifestSetOperation(
                            "field of view",
                            JsonElementFactory.Number(90.0),
                            JsonElementFactory.Number(75.0)),
                    ],
                    null),
            ]));
        var assetsFileService = new StubAssetsFileService(
            [new AssetsInfo(10, 20, "Camera", 128)],
            new Dictionary<long, AssetsFieldInfo>
            {
                [10] = new("Camera", "Camera", null,
                [
                    new AssetsFieldInfo("m_Name", "string", "Main Camera", []),
                    new AssetsFieldInfo("field of view", "float", "90.0", []),
                ]),
            });
        var service = new AssetsWorkflowService(assetsFileService, assetsFileService, manifestLoader);

        PatchPreviewResult preview = service.PreviewPatch(new PatchPreviewRequest("resources.assets", configPath));

        PatchPreviewAssetResult asset = Assert.Single(preview.Assets);
        Assert.Equal(10, asset.Asset.PathId);
        PatchPreviewOperationResult operation = Assert.Single(asset.Operations);
        Assert.Equal("field of view", operation.Path);
        Assert.True(operation.WillChange);
        Assert.Equal(configPath, manifestLoader.ConfigPath);
    }

    [Fact]
    public void PreviewPatch_WhenMultipleTargetsUseSameAssetsFile_ReadsAssetsInfoOnce()
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
                new ManifestPatch(
                    "resources.assets",
                    "Camera",
                    [
                        new Dictionary<string, JsonElement>
                        {
                            ["m_Name"] = JsonElementFactory.String("Main Camera"),
                        },
                    ],
                    [
                        new ManifestSetOperation(
                            "field of view",
                            JsonElementFactory.Number(45L),
                            JsonElementFactory.Number(60L)),
                    ],
                    null),
                new ManifestPatch(
                    "resources.assets",
                    "Camera",
                    [
                        new Dictionary<string, JsonElement>
                        {
                            ["m_Name"] = JsonElementFactory.String("Secondary Camera"),
                        },
                    ],
                    [
                        new ManifestSetOperation(
                            "field of view",
                            JsonElementFactory.Number(30L),
                            JsonElementFactory.Number(75L)),
                    ],
                    null),
            ]));
        var assetsFileService = new StubAssetsFileService(
            [
                new AssetsInfo(10, 20, "Camera", 128),
                new AssetsInfo(11, 20, "Camera", 128),
            ],
            new Dictionary<long, AssetsFieldInfo>
            {
                [10] = new("Camera", "Camera", null,
                [
                    new AssetsFieldInfo("m_Name", "string", "Main Camera", []),
                    new AssetsFieldInfo("field of view", "float", "45", []),
                ]),
                [11] = new("Camera", "Camera", null,
                [
                    new AssetsFieldInfo("m_Name", "string", "Secondary Camera", []),
                    new AssetsFieldInfo("field of view", "float", "30", []),
                ]),
            });
        var service = new AssetsWorkflowService(assetsFileService, assetsFileService, manifestLoader);

        PatchPreviewResult preview = service.PreviewPatch(new PatchPreviewRequest("resources.assets", configPath));

        Assert.Equal([10L, 11L], preview.Assets.Select(asset => asset.Asset.PathId));
        Assert.Equal(1, assetsFileService.ReadAssetsInfoCallCount);
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
        var service = new AssetsWorkflowService(new StubAssetsFileService(
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
        var service = new AssetsWorkflowService(new StubAssetsFileService(
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
    /// Verifies that apply writes the patch plan and returns an output summary when all set operations can change.
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
        var assetsFileService = new StubAssetsFileService(
            [new AssetsInfo(4, 20, "Camera", 128)],
            new Dictionary<long, AssetsFieldInfo>
            {
                [4] = new("Camera", "Camera", null, [new AssetsFieldInfo("field of view", "float", "90.0", [])]),
            });
        var service = new AssetsWorkflowService(assetsFileService);

        try
        {
            PatchApplyResult result =
                service.ApplyPatch(new PatchApplyRequest(inputPath, configPath, outputPath, backupDirectory));

            Assert.Equal(outputPath, result.OutputPath);
            Assert.Null(result.BackupPath);
            Assert.Equal(1, result.AssetCount);
            Assert.Equal(1, result.OperationCount);
            Assert.Equal(inputPath, assetsFileService.InputPath);
            Assert.Equal(outputPath, assetsFileService.OutputPath);
            PatchWriteAsset asset = Assert.Single(assetsFileService.Plan);
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
    /// Verifies that replaceFrom copies full source assets into matching target assets by the configured field.
    /// </summary>
    [Fact]
    public void ApplyPatch_WhenReplaceFromMatchesIncludedAudioClips_WritesReplacementPlan()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string configPath = Path.Combine(tempDirectory, "manifest.json");
        string inputPath = Path.Combine(tempDirectory, "sharedassets4.assets");
        string outputPath = Path.Combine(tempDirectory, "sharedassets4.patched.assets");
        string sourceDirectory = Path.Combine(tempDirectory, "resources");
        string sourcePath = Path.Combine(sourceDirectory, "modassets.assets");
        string backupDirectory = Path.Combine(tempDirectory, "backup");
        Directory.CreateDirectory(sourceDirectory);
        File.WriteAllText(inputPath, "original");
        File.WriteAllText(sourcePath, "source");
        TestManifest.Write(
            configPath,
            """
            {
              "target": "sharedassets4.assets",
              "type": "AudioClip",
              "include": [
                {
                  "m_Name": "Incense burn 1"
                },
                {
                  "m_Name": "Crucifix Burn Start"
                }
              ],
              "replaceFrom": {
                "assets": "resources/modassets.assets",
                "match": "m_Name"
              }
            }
            """);
        var assetsFileService = new StubAssetsFileService(
            new Dictionary<string, IReadOnlyList<AssetsInfo>>(StringComparer.OrdinalIgnoreCase)
            {
                [inputPath] =
                [
                    new AssetsInfo(100, 83, "AudioClip", 128),
                    new AssetsInfo(101, 83, "AudioClip", 128),
                    new AssetsInfo(102, 83, "AudioClip", 128),
                ],
                [sourcePath] =
                [
                    new AssetsInfo(200, 83, "AudioClip", 128),
                    new AssetsInfo(201, 83, "AudioClip", 128),
                ],
            },
            new Dictionary<(string AssetsFilePath, long PathId), AssetsFieldInfo>
            {
                [(inputPath, 100)] = CreateAudioClipFieldTree("Incense burn 1"),
                [(inputPath, 101)] = CreateAudioClipFieldTree("Crucifix Burn Start"),
                [(inputPath, 102)] = CreateAudioClipFieldTree("Unrelated"),
                [(sourcePath, 200)] = CreateAudioClipFieldTree("Incense burn 1"),
                [(sourcePath, 201)] = CreateAudioClipFieldTree("Crucifix Burn Start"),
            });
        var service = new AssetsWorkflowService(assetsFileService);

        try
        {
            PatchApplyResult result =
                service.ApplyPatch(new PatchApplyRequest(inputPath, configPath, outputPath, backupDirectory));

            Assert.Equal(outputPath, result.OutputPath);
            Assert.Null(result.BackupPath);
            Assert.Equal(2, result.AssetCount);
            Assert.Equal(2, result.OperationCount);
            Assert.Equal(inputPath, assetsFileService.InputPath);
            Assert.Equal(outputPath, assetsFileService.OutputPath);
            Assert.Equal(
                [
                    new AssetReplacement(sourcePath, 200, 100),
                    new AssetReplacement(sourcePath, 201, 101),
                ],
                assetsFileService.ReplacementPlan);
            Assert.False(assetsFileService.Plan.Any());
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, true);
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
        var assetsFileService = new StubAssetsFileService(
            [
                new AssetsInfo(4, 20, "Camera", 128),
                new AssetsInfo(5, 108, "Light", 96),
            ],
            new Dictionary<long, AssetsFieldInfo>
            {
                [4] = new("Camera", "Camera", null, [new AssetsFieldInfo("field of view", "float", "90.0", [])]),
                [5] = new("Light", "Light", null, [new AssetsFieldInfo("m_Intensity", "float", "1.0", [])]),
            });
        var service = new AssetsWorkflowService(assetsFileService);

        try
        {
            PatchApplyResult result =
                service.ApplyPatch(new PatchApplyRequest(inputPath, configPath, outputPath, backupDirectory));

            Assert.Equal(2, result.AssetCount);
            Assert.Equal(2, result.OperationCount);
            Assert.Equal([4L, 5L], assetsFileService.Plan.Select(asset => asset.PathId));
            Assert.Equal("field of view", Assert.Single(assetsFileService.Plan[0].Operations).Path);
            Assert.Equal("m_Intensity", Assert.Single(assetsFileService.Plan[1].Operations).Path);
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
        var assetsFileService = new StubAssetsFileService(
            [new AssetsInfo(4, 20, "Camera", 128)],
            new Dictionary<long, AssetsFieldInfo>
            {
                [4] = new("Camera", "Camera", null,
                [
                    new AssetsFieldInfo("field of view", "float", "90.0", []),
                    new AssetsFieldInfo("near clip plane", "float", "0.3", []),
                ]),
            });
        var service = new AssetsWorkflowService(assetsFileService);

        try
        {
            PatchApplyResult result =
                service.ApplyPatch(new PatchApplyRequest(inputPath, configPath, outputPath, backupDirectory));

            Assert.Equal(1, result.AssetCount);
            Assert.Equal(2, result.OperationCount);
            PatchWriteAsset asset = Assert.Single(assetsFileService.Plan);
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
        var assetsFileService = new StubAssetsFileService(
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
            });
        var service = new AssetsWorkflowService(assetsFileService);

        try
        {
            PatchApplyResult result =
                service.ApplyPatch(new PatchApplyRequest(inputPath, configPath, outputPath, backupDirectory));

            Assert.Equal(1, result.AssetCount);
            Assert.Equal(4, result.OperationCount);
            PatchWriteAsset asset = Assert.Single(assetsFileService.Plan);
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
    /// Verifies that patch can add a value to a Unity string array represented as an Array child.
    /// </summary>
    [Fact]
    public void ApplyPatch_WhenSetAddsStringArrayItem_WritesArrayFieldOperation()
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
                    "field": "m_ValidKeywords",
                    "from": {
                      "Array": [
                        "_METALLICSPECGLOSSMAP",
                        "_NORMALMAP"
                      ]
                    },
                    "to": {
                      "Array": [
                        "_METALLICSPECGLOSSMAP",
                        "_NORMALMAP",
                        "_EMISSION"
                      ]
                    }
                  }
                ]
              }
              """);
        var assetsFileService = new StubAssetsFileService(
            [new AssetsInfo(9, 21, "Material", 96)],
            new Dictionary<long, AssetsFieldInfo>
            {
                [9] = new("Material", "Material", null,
                [
                    new AssetsFieldInfo("m_Name", "string", "TargetMaterial", []),
                    new AssetsFieldInfo("m_ValidKeywords", "vector", null,
                    [
                        new AssetsFieldInfo("Array", "Array", null,
                        [
                            new AssetsFieldInfo("data", "string", "_METALLICSPECGLOSSMAP", []),
                            new AssetsFieldInfo("data", "string", "_NORMALMAP", []),
                        ]),
                    ]),
                ]),
            });
        var service = new AssetsWorkflowService(assetsFileService);

        try
        {
            PatchApplyResult result =
                service.ApplyPatch(new PatchApplyRequest(inputPath, configPath, outputPath, backupDirectory));

            Assert.Equal(1, result.AssetCount);
            Assert.Equal(1, result.OperationCount);
            PatchWriteOperation operation = Assert.Single(Assert.Single(assetsFileService.Plan).Operations);
            Assert.Equal("m_ValidKeywords.Array", operation.Path);
            Assert.Equal(
                ["_METALLICSPECGLOSSMAP", "_NORMALMAP", "_EMISSION"],
                operation.To.EnumerateArray().Select(element => element.GetString()));
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
    /// Verifies that add preview still reports an array operation when the value already exists and no write is needed.
    /// </summary>
    [Fact]
    public void PreviewPatch_WhenAddValueAlreadyExists_ReturnsSkippedArrayOperation()
    {
        string configPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        TestManifest.Write(
            configPath,
            """
            {
              "target": "resources.assets",
              "type": "Material",
              "include": [
                {
                  "m_Name": "Bone Parts"
                }
              ],
              "add": [
                {
                  "field": "m_ValidKeywords.Array",
                  "value": [
                    "_EMISSION"
                  ]
                }
              ]
            }
            """);
        var service = new AssetsWorkflowService(new StubAssetsFileService(
            [new AssetsInfo(10, 21, "Material", 96)],
            new Dictionary<long, AssetsFieldInfo>
            {
                [10] = new("Material", "Material", null,
                [
                    new AssetsFieldInfo("m_Name", "string", "Bone Parts", []),
                    new AssetsFieldInfo("m_ValidKeywords", "vector", null,
                    [
                        new AssetsFieldInfo("Array", "Array", null,
                        [
                            new AssetsFieldInfo("data", "string", "_METALLICSPECGLOSSMAP", []),
                            new AssetsFieldInfo("data", "string", "_EMISSION", []),
                        ]),
                    ]),
                ]),
            }));

        try
        {
            PatchPreviewResult preview = service.PreviewPatch(new PatchPreviewRequest("resources.assets", configPath));

            PatchPreviewOperationResult operation = Assert.Single(Assert.Single(preview.Assets).Operations);
            Assert.False(operation.WillChange);
            Assert.Equal("m_ValidKeywords.Array", operation.Path);
            Assert.Equal("[\"_METALLICSPECGLOSSMAP\", \"_EMISSION\"]", operation.OldValue);
            Assert.Equal(["_METALLICSPECGLOSSMAP", "_EMISSION"],
                operation.To.EnumerateArray().Select(element => element.GetString()));
        }
        finally
        {
            File.Delete(configPath);
        }
    }

    /// <summary>
    /// Verifies that add appends missing values to a Unity array without requiring a full from/to replacement.
    /// </summary>
    [Fact]
    public void ApplyPatch_WhenAddValueIsMissing_WritesArrayFieldOperation()
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
                    "m_Name": "Bone Parts"
                  }
                ],
                "add": [
                  {
                    "field": "m_ValidKeywords.Array",
                    "value": [
                      "_EMISSION"
                    ]
                  }
                ]
              }
              """);
        var assetsFileService = new StubAssetsFileService(
            [new AssetsInfo(10, 21, "Material", 96)],
            new Dictionary<long, AssetsFieldInfo>
            {
                [10] = new("Material", "Material", null,
                [
                    new AssetsFieldInfo("m_Name", "string", "Bone Parts", []),
                    new AssetsFieldInfo("m_ValidKeywords", "vector", null,
                    [
                        new AssetsFieldInfo("Array", "Array", null,
                        [
                            new AssetsFieldInfo("data", "string", "_METALLICSPECGLOSSMAP", []),
                            new AssetsFieldInfo("data", "string", "_NORMALMAP", []),
                        ]),
                    ]),
                ]),
            });
        var service = new AssetsWorkflowService(assetsFileService);

        try
        {
            PatchApplyResult result =
                service.ApplyPatch(new PatchApplyRequest(inputPath, configPath, outputPath, backupDirectory));

            Assert.Equal(1, result.AssetCount);
            Assert.Equal(1, result.OperationCount);
            PatchWriteOperation operation = Assert.Single(Assert.Single(assetsFileService.Plan).Operations);
            Assert.Equal("m_ValidKeywords.Array", operation.Path);
            Assert.Equal(
                ["_METALLICSPECGLOSSMAP", "_NORMALMAP", "_EMISSION"],
                operation.To.EnumerateArray().Select(element => element.GetString()));
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
    /// Verifies that add skips values that already exist in the target Unity array.
    /// </summary>
    [Fact]
    public void ApplyPatch_WhenAddValueAlreadyExists_SkipsWithoutWriting()
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
                    "m_Name": "Bone Parts"
                  }
                ],
                "add": [
                  {
                    "field": "m_ValidKeywords.Array",
                    "value": [
                      "_EMISSION"
                    ]
                  }
                ]
              }
              """);
        var assetsFileService = new StubAssetsFileService(
            [new AssetsInfo(10, 21, "Material", 96)],
            new Dictionary<long, AssetsFieldInfo>
            {
                [10] = new("Material", "Material", null,
                [
                    new AssetsFieldInfo("m_Name", "string", "Bone Parts", []),
                    new AssetsFieldInfo("m_ValidKeywords", "vector", null,
                    [
                        new AssetsFieldInfo("Array", "Array", null,
                        [
                            new AssetsFieldInfo("data", "string", "_METALLICSPECGLOSSMAP", []),
                            new AssetsFieldInfo("data", "string", "_NORMALMAP", []),
                            new AssetsFieldInfo("data", "string", "_EMISSION", []),
                        ]),
                    ]),
                ]),
            });
        var service = new AssetsWorkflowService(assetsFileService);

        try
        {
            PatchApplyResult result =
                service.ApplyPatch(new PatchApplyRequest(inputPath, configPath, outputPath, backupDirectory));

            Assert.Equal(0, result.AssetCount);
            Assert.Equal(0, result.OperationCount);
            Assert.False(assetsFileService.WasCalled);
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
                "targets": [
                  {
                    "file": "{{Path.GetFileName(inputPath)}}",
                    "patches": [
                      {
                        "type": "Material",
                        "match": {
                          "m_Name": "TargetMaterial"
                        },
                        "set": {
                          "m_SavedProperties.m_TexEnvs.Array.data[first=_EmissionMap].second.m_Texture.m_PathID": {
                            "from": 0,
                            "to": {
                              "$pathId": {
                                "type": "Texture2D",
                                "match": {
                                  "m_Name": "EmissionTexture"
                                }
                              }
                            }
                          }
                        }
                      }
                    ]
                  }
                ]
              }
              """);
        var assetsFileService = new StubAssetsFileService(
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
            });
        var service = new AssetsWorkflowService(assetsFileService);

        try
        {
            PatchApplyResult result =
                service.ApplyPatch(new PatchApplyRequest(inputPath, configPath, outputPath, backupDirectory));

            Assert.Equal(1, result.AssetCount);
            Assert.Equal(1, result.OperationCount);
            PatchWriteOperation operation = Assert.Single(Assert.Single(assetsFileService.Plan).Operations);
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
        var assetsFileService = new StubAssetsFileService(
            [new AssetsInfo(4, 20, "Camera", 128)],
            new Dictionary<long, AssetsFieldInfo>
            {
                [4] = new("Camera", "Camera", null, [new AssetsFieldInfo("field of view", "float", "90.0", [])]),
            });
        var service = new AssetsWorkflowService(assetsFileService);

        try
        {
            var exception = Assert.Throws<InvalidOperationException>(() =>
                service.ApplyPatch(new PatchApplyRequest(inputPath, configPath, outputPath, Path.GetTempPath())));

            Assert.Contains("cannot be applied", exception.Message);
            Assert.False(assetsFileService.WasCalled);
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
        var assetsFileService = new StubAssetsFileService(
            [new AssetsInfo(4, 20, "Camera", 128)],
            new Dictionary<long, AssetsFieldInfo>
            {
                [4] = new("Camera", "Camera", null, [new AssetsFieldInfo("field of view", "float", "90.0", [])]),
            });
        var service = new AssetsWorkflowService(assetsFileService);

        try
        {
            var exception = Assert.Throws<IOException>(() =>
                service.ApplyPatch(new PatchApplyRequest(inputPath, configPath, outputPath, Path.GetTempPath())));

            Assert.Contains("already exists", exception.Message);
            Assert.False(assetsFileService.WasCalled);
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
        var assetsFileService = new StubAssetsFileService(
            [new AssetsInfo(4, 20, "Camera", 128)],
            new Dictionary<long, AssetsFieldInfo>
            {
                [4] = new("Camera", "Camera", null, [new AssetsFieldInfo("field of view", "float", "90.0", [])]),
            });
        var service = new AssetsWorkflowService(assetsFileService);

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
        var service = new AssetsWorkflowService(new StubAssetsFileService(
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
        var assetsFileService = new StubAssetsFileService(
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
            });
        var service = new AssetsWorkflowService(assetsFileService);

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
            Assert.Equal(targetPath, assetsFileService.InputPath);
            Assert.Equal(targetPath, assetsFileService.OutputPath);
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
    /// Verifies that install copies declared zip payload files beside the resolved assets file.
    /// </summary>
    [Fact]
    public void InstallMod_WhenManifestHasFiles_CopiesZipEntriesToAssetsDirectory()
    {
        string zipPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.zip");
        string gameDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string targetDirectory = Path.Combine(gameDirectory, "Game_Data");
        string targetPath = Path.Combine(targetDirectory, "sharedassets4.assets");
        string backupDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string copiedPath = Path.Combine(targetDirectory, "modassets.resource");
        Directory.CreateDirectory(targetDirectory);
        File.WriteAllText(targetPath, "original");

        using (ZipArchive archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            ZipArchiveEntry manifestEntry = archive.CreateEntry("Mod/manifest.json");
            using (StreamWriter writer = new(manifestEntry.Open()))
            {
                writer.Write(TestManifest.CreateJson(
                    """
                    {
                      "name": "Test Mod",
                      "author": "UnityAssetsPatcher.Tests",
                      "version": "1.0.0",
                      "files": [
                        {
                          "source": "resources/modassets.resource"
                        }
                      ],
                      "patches": [
                        {
                          "target": "sharedassets4.assets",
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
                    """));
            }

            ZipArchiveEntry payloadEntry = archive.CreateEntry("resources/modassets.resource");
            using StreamWriter payloadWriter = new(payloadEntry.Open());
            payloadWriter.Write("payload");
        }

        var assetsFileService = new StubAssetsFileService(
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
            });
        var service = new AssetsWorkflowService(assetsFileService);

        try
        {
            InstallModResult result = service.InstallMod(
                new InstallModRequest(zipPath, gameDirectory, backupDirectory));

            InstallCopiedFileResult copiedFile = Assert.Single(result.CopiedFiles);
            Assert.Equal("resources/modassets.resource", copiedFile.Source);
            Assert.Equal(copiedPath, copiedFile.DestinationPath);
            Assert.Equal("payload", File.ReadAllText(copiedPath));
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
    /// Verifies that install can use replaceFrom assets stored inside the mod zip.
    /// </summary>
    [Fact]
    public void InstallMod_WhenReplaceFromUsesZipEntry_ExtractsSourceAssetsAndWritesReplacementPlan()
    {
        string zipPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.zip");
        string gameDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string targetDirectory = Path.Combine(gameDirectory, "Game_Data");
        string targetPath = Path.Combine(targetDirectory, "sharedassets4.assets");
        string backupDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(targetDirectory);
        File.WriteAllText(targetPath, "original");

        using (ZipArchive archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            ZipArchiveEntry manifestEntry = archive.CreateEntry("Mod/manifest.json");
            using (StreamWriter writer = new(manifestEntry.Open()))
            {
                writer.Write(TestManifest.CreateJson(
                    """
                    {
                      "name": "Test Mod",
                      "author": "UnityAssetsPatcher.Tests",
                      "version": "1.0.0",
                      "patches": [
                        {
                          "target": "sharedassets4.assets",
                          "type": "AudioClip",
                          "include": [
                            {
                              "m_Name": "Incense burn 1"
                            }
                          ],
                          "replaceFrom": {
                            "assets": "resources/modassets.assets",
                            "match": "m_Name"
                          }
                        }
                      ]
                    }
                    """));
            }

            ZipArchiveEntry sourceAssetsEntry = archive.CreateEntry("resources/modassets.assets");
            using StreamWriter sourceAssetsWriter = new(sourceAssetsEntry.Open());
            sourceAssetsWriter.Write("source assets");
        }

        var assetsFileService = new StubAssetsFileService(
            new Dictionary<string, IReadOnlyList<AssetsInfo>>(StringComparer.OrdinalIgnoreCase)
            {
                [targetPath] =
                [
                    new AssetsInfo(100, 83, "AudioClip", 128),
                ],
                ["modassets.assets"] =
                [
                    new AssetsInfo(200, 83, "AudioClip", 128),
                ],
            },
            new Dictionary<(string AssetsFilePath, long PathId), AssetsFieldInfo>
            {
                [(targetPath, 100)] = CreateAudioClipFieldTree("Incense burn 1"),
                [("modassets.assets", 200)] = CreateAudioClipFieldTree("Incense burn 1"),
            });
        var service = new AssetsWorkflowService(assetsFileService);

        try
        {
            InstallModResult result = service.InstallMod(
                new InstallModRequest(zipPath, gameDirectory, backupDirectory));

            InstallModFileResult file = Assert.Single(result.Files);
            Assert.Equal(1, file.AssetCount);
            Assert.Equal(1, file.OperationCount);
            AssetReplacement replacement = Assert.Single(assetsFileService.ReplacementPlan);
            Assert.Equal(200, replacement.SourcePathId);
            Assert.Equal(100, replacement.TargetPathId);
            Assert.Equal("modassets.assets", Path.GetFileName(replacement.SourceAssetsFilePath));
            Assert.Equal("patched", File.ReadAllText(targetPath));
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
    /// Verifies that install preview locates assets files from zip manifest targets without writing files.
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
        var service = new AssetsWorkflowService(new StubAssetsFileService(
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
    /// Verifies that install preview can resolve the game directory from the manifest game field when no directory is provided.
    /// </summary>
    [Fact]
    public void PreviewInstallMod_WhenManifestHasGameAndNoDirectory_UsesResolvedGameDirectory()
    {
        string zipPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.zip");
        string steamDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "Steam");
        string steamAppsDirectory = Path.Combine(steamDirectory, "steamapps");
        string gameDirectory = Path.Combine(steamAppsDirectory, "common", "Phasmophobia");
        string targetDirectory = Path.Combine(gameDirectory, "Game_Data");
        string targetPath = Path.Combine(targetDirectory, "sharedassets0.assets");
        Directory.CreateDirectory(targetDirectory);
        File.WriteAllText(targetPath, "original");
        File.WriteAllText(
            Path.Combine(steamAppsDirectory, "appmanifest_739630.acf"),
            """
            "AppState"
            {
                "name" "Phasmophobia"
                "installdir" "Phasmophobia"
            }
            """);
        TestManifest.WriteZip(
            zipPath,
            """
            {
              "game": "Phasmophobia",
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
        var service = new AssetsWorkflowService(
            new StubAssetsFileService(
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
            new GameDirectoryResolver([steamDirectory]));

        try
        {
            InstallPreviewResult result = service.PreviewInstallMod(
                new InstallPreviewRequest(zipPath, null));

            InstallPreviewFileResult file = Assert.Single(result.Files);
            Assert.Equal(targetPath, file.AssetsFilePath);
        }
        finally
        {
            File.Delete(zipPath);
            Directory.Delete(Path.GetDirectoryName(steamDirectory)!, true);
        }
    }

    /// <summary>
    /// Verifies that install preview gives a clear error when neither manual directory nor manifest game can resolve a directory.
    /// </summary>
    [Fact]
    public void PreviewInstallMod_WhenNoDirectoryAndNoResolvedGame_ThrowsClearError()
    {
        string zipPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.zip");
        TestManifest.WriteZip(
            zipPath,
            """
            {
              "game": "Missing Game",
              "patches": [
                {
                  "target": "sharedassets0.assets",
                  "type": "Camera",
                  "match": {
                    "field of view": 90.0
                  }
                }
              ]
            }
            """);
        var service = new AssetsWorkflowService(
            new StubAssetsFileService([]),
            new GameDirectoryResolver([]));

        try
        {
            var exception = Assert.Throws<DirectoryNotFoundException>(() =>
                service.PreviewInstallMod(new InstallPreviewRequest(zipPath, null)));

            Assert.Contains("Game directory could not be resolved", exception.Message);
            Assert.Contains("Missing Game", exception.Message);
        }
        finally
        {
            File.Delete(zipPath);
        }
    }

    /// <summary>
    /// Verifies that install preview reports payload file copies without writing them.
    /// </summary>
    [Fact]
    public void PreviewInstallMod_WhenManifestHasFiles_ReturnsCopyPlanWithoutWritingFiles()
    {
        string zipPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.zip");
        string gameDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string targetDirectory = Path.Combine(gameDirectory, "Game_Data");
        string targetPath = Path.Combine(targetDirectory, "sharedassets4.assets");
        string copiedPath = Path.Combine(targetDirectory, "modassets.resource");
        Directory.CreateDirectory(targetDirectory);
        File.WriteAllText(targetPath, "original");

        using (ZipArchive archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            ZipArchiveEntry manifestEntry = archive.CreateEntry("Mod/manifest.json");
            using (StreamWriter writer = new(manifestEntry.Open()))
            {
                writer.Write(TestManifest.CreateJson(
                    """
                    {
                      "name": "Test Mod",
                      "author": "UnityAssetsPatcher.Tests",
                      "version": "1.0.0",
                      "files": [
                        {
                          "source": "resources/modassets.resource"
                        }
                      ],
                      "patches": [
                        {
                          "target": "sharedassets4.assets",
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
                    """));
            }

            ZipArchiveEntry payloadEntry = archive.CreateEntry("resources/modassets.resource");
            using StreamWriter payloadWriter = new(payloadEntry.Open());
            payloadWriter.Write("payload");
        }

        var service = new AssetsWorkflowService(new StubAssetsFileService(
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

            InstallCopyFilePreviewResult copiedFile = Assert.Single(result.CopiedFiles);
            Assert.Equal("resources/modassets.resource", copiedFile.Source);
            Assert.Equal(copiedPath, copiedFile.DestinationPath);
            Assert.True(copiedFile.WillCopy);
            Assert.False(File.Exists(copiedPath));
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
        var assetsFileService = new StubAssetsFileService(
            [new AssetsInfo(4, 20, "Camera", 128)],
            new Dictionary<long, AssetsFieldInfo>
            {
                [4] = new("Camera", "Camera", null, [new AssetsFieldInfo("field of view", "float", "90.0", [])]),
            });
        var service = new AssetsWorkflowService(assetsFileService);

        try
        {
            var exception = Assert.Throws<InvalidOperationException>(() =>
                service.InstallMod(new InstallModRequest(zipPath, gameDirectory, backupDirectory)));

            Assert.Contains("matched multiple files", exception.Message);
            Assert.False(assetsFileService.WasCalled);
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

    private static AssetsFieldInfo CreateAudioClipFieldTree(string name)
    {
        return new AssetsFieldInfo(
            "AudioClip",
            "AudioClip",
            null,
            [
                new AssetsFieldInfo("m_Name", "string", name, []),
            ]);
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

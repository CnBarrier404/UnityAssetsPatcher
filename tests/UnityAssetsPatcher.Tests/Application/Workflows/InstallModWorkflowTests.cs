using System.IO.Compression;
using UnityAssetsPatcher.Application;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Manifests;
using UnityAssetsPatcher.Application.Patching;
using UnityAssetsPatcher.Application.Workflows;
using UnityAssetsPatcher.Core.Assets;
using UnityAssetsPatcher.Tests.Support;
using Xunit;

namespace UnityAssetsPatcher.Tests.Application.Workflows;

public sealed class InstallModWorkflowTests
{
    /// <summary>
    /// Verifies that install locates assets files from zip manifest targets under the game directory and writes in place.
    /// </summary>
    [Fact]
    public void Install_WhenZipTargetMatchesSingleFile_OverwritesTargetAndReturnsSummary()
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
        var workflow = CreateWorkflow(assetsFileService);

        try
        {
            InstallModResult result = workflow.Install(
                new InstallModRequest(zipPath, gameDirectory, backupDirectory));

            Assert.Equal("Test Mod", result.ModName);
            Assert.Equal("UnityAssetsPatcher.Tests", result.ModAuthor);
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
    public void Install_WhenManifestHasFiles_CopiesZipEntriesToAssetsDirectory()
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
        var workflow = CreateWorkflow(assetsFileService);

        try
        {
            InstallModResult result = workflow.Install(
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
    public void Install_WhenReplaceFromUsesZipEntry_ExtractsSourceAssetsAndWritesReplacementPlan()
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
                              "m_Name": "Example Clip"
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
                [(targetPath, 100)] = CreateAudioClipFieldTree("Example Clip"),
                [("modassets.assets", 200)] = CreateAudioClipFieldTree("Example Clip"),
            });
        var workflow = CreateWorkflow(assetsFileService);

        try
        {
            InstallModResult result = workflow.Install(
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
    /// Verifies that install closes the package after loading and reopens it only when payload files are copied.
    /// </summary>
    [Fact]
    public void Install_WhenZipHasReplacementSourcesAndPayload_ReopensPackageForPayloadCopy()
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
                          "type": "AudioClip",
                          "include": [
                            {
                              "m_Name": "Example Clip"
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
            using (StreamWriter sourceAssetsWriter = new(sourceAssetsEntry.Open()))
            {
                sourceAssetsWriter.Write("source assets");
            }

            ZipArchiveEntry payloadEntry = archive.CreateEntry("resources/modassets.resource");
            using StreamWriter payloadWriter = new(payloadEntry.Open());
            payloadWriter.Write("payload");
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
                [(targetPath, 100)] = CreateAudioClipFieldTree("Example Clip"),
                [("modassets.assets", 200)] = CreateAudioClipFieldTree("Example Clip"),
            });
        var workflow = CreateWorkflow(assetsFileService);

        try
        {
            InstallModResult result = workflow.Install(
                new InstallModRequest(zipPath, gameDirectory, backupDirectory));

            Assert.Single(result.Files);
            Assert.Single(result.CopiedFiles);
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
    /// Verifies that install preview locates assets files from zip manifest targets without writing files.
    /// </summary>
    [Fact]
    public void Preview_WhenZipTargetMatchesSingleFile_ReturnsDryRunResultsWithoutWriter()
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
        var workflow = CreateWorkflow(new StubAssetsFileService(
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
            InstallPreviewResult result = workflow.Preview(
                new InstallPreviewRequest(zipPath, gameDirectory));

            Assert.Equal("Test Mod", result.ModName);
            Assert.Equal("UnityAssetsPatcher.Tests", result.ModAuthor);
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
    /// Verifies that reusing one workflow for preview and install still releases read resources before writing.
    /// </summary>
    [Fact]
    public void Install_WhenSameWorkflowPreviewedFirst_ReleasesReadResourcesAgainBeforeWriting()
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
        var workflow = CreateWorkflow(assetsFileService);

        try
        {
            workflow.Preview(new InstallPreviewRequest(zipPath, gameDirectory));
            workflow.Install(new InstallModRequest(zipPath, gameDirectory, backupDirectory));

            Assert.True(assetsFileService.DisposeCountAtWrite >= 2);
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
    /// Verifies that install preview can resolve the game directory from the manifest game field when no directory is provided.
    /// </summary>
    [Fact]
    public void Preview_WhenManifestHasGameAndNoDirectory_UsesResolvedGameDirectory()
    {
        string zipPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.zip");
        string steamDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "Steam");
        string steamAppsDirectory = Path.Combine(steamDirectory, "steamapps");
        string gameDirectory = Path.Combine(steamAppsDirectory, "common", "Example Game");
        string targetDirectory = Path.Combine(gameDirectory, "Game_Data");
        string targetPath = Path.Combine(targetDirectory, "sharedassets0.assets");
        Directory.CreateDirectory(targetDirectory);
        File.WriteAllText(targetPath, "original");
        File.WriteAllText(
            Path.Combine(steamAppsDirectory, "appmanifest_123456.acf"),
            """
            "AppState"
            {
                "name" "Example Game"
                "installdir" "Example Game"
            }
            """);
        TestManifest.WriteZip(
            zipPath,
            """
            {
              "game": "Example Game",
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
        var workflow = CreateWorkflow(
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
            [steamDirectory]);

        try
        {
            InstallPreviewResult result = workflow.Preview(
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
    public void Preview_WhenNoDirectoryAndNoResolvedGame_ThrowsClearError()
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
        var workflow = CreateWorkflow(
            new StubAssetsFileService([]),
            []);

        try
        {
            var exception = Assert.Throws<DirectoryNotFoundException>(() =>
                workflow.Preview(new InstallPreviewRequest(zipPath, null)));

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
    public void Preview_WhenManifestHasFiles_ReturnsCopyPlanWithoutWritingFiles()
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

        var workflow = CreateWorkflow(new StubAssetsFileService(
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
            InstallPreviewResult result = workflow.Preview(
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
    public void Install_WhenTargetMatchesMultipleFiles_ThrowsWithoutWriting()
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
        var workflow = CreateWorkflow(assetsFileService);

        try
        {
            var exception = Assert.Throws<InvalidOperationException>(() =>
                workflow.Install(new InstallModRequest(zipPath, gameDirectory, backupDirectory)));

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

    /// <summary>
    /// Verifies that replacement planning resolves source paths from the explicit source path map,
    /// not from a fake manifest.json path or directory derivation.
    /// </summary>
    [Fact]
    public void Install_WhenReplaceFromUsesZipEntry_SourcePathResolvedFromExplicitMap()
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
                              "m_Name": "Example Clip"
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
                [(targetPath, 100)] = CreateAudioClipFieldTree("Example Clip"),
                [("modassets.assets", 200)] = CreateAudioClipFieldTree("Example Clip"),
            });
        var workflow = CreateWorkflow(assetsFileService);

        try
        {
            workflow.Install(new InstallModRequest(zipPath, gameDirectory, backupDirectory));

            AssetReplacement replacement = Assert.Single(assetsFileService.ReplacementPlan);
            Assert.StartsWith(Path.GetTempPath(), replacement.SourceAssetsFilePath, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("UnityAssetsPatcher.", replacement.SourceAssetsFilePath);
            Assert.EndsWith("modassets.assets", replacement.SourceAssetsFilePath);
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
    /// Verifies that when source extraction fails midway, the temporary directory is cleaned up.
    /// Uses a before/after diff to avoid interfering with concurrent runs.
    /// </summary>
    [Fact]
    public void Install_WhenSecondSourceEntryMissing_DeletesTemporaryDirectory()
    {
        string zipPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.zip");
        string gameDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string targetDirectory = Path.Combine(gameDirectory, "Game_Data");
        string targetPath = Path.Combine(targetDirectory, "sharedassets4.assets");
        string backupDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(targetDirectory);
        File.WriteAllText(targetPath, "original");

        HashSet<string> before = Directory.GetDirectories(Path.GetTempPath(), "UnityAssetsPatcher.*")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

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
                              "m_Name": "Example Clip"
                            }
                          ],
                          "replaceFrom": {
                            "assets": "resources/modassets.assets",
                            "match": "m_Name"
                          }
                        },
                        {
                          "target": "sharedassets4.assets",
                          "type": "AudioClip",
                          "include": [
                            {
                              "m_Name": "Missing clip"
                            }
                          ],
                          "replaceFrom": {
                            "assets": "resources/missing.assets",
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
            },
            new Dictionary<(string AssetsFilePath, long PathId), AssetsFieldInfo>
            {
                [(targetPath, 100)] = CreateAudioClipFieldTree("Example Clip"),
            });
        var workflow = CreateWorkflow(assetsFileService);

        try
        {
            Assert.ThrowsAny<Exception>(() =>
                workflow.Install(new InstallModRequest(zipPath, gameDirectory, backupDirectory)));

            HashSet<string> after = Directory.GetDirectories(Path.GetTempPath(), "UnityAssetsPatcher.*")
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            after.ExceptWith(before);
            Assert.Empty(after);
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

            HashSet<string> cleanup = Directory.GetDirectories(Path.GetTempPath(), "UnityAssetsPatcher.*")
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            cleanup.ExceptWith(before);

            foreach (string dir in cleanup)
            {
                Directory.Delete(dir, true);
            }
        }
    }

    /// <summary>
    /// Verifies that preview reports declared optional groups and that selecting one merges its target into the plan.
    /// </summary>
    [Fact]
    public void Preview_WhenManifestHasOptionalGroups_ReportsGroupsAndMergesSelected()
    {
        string zipPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.zip");
        string gameDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string targetDirectory = Path.Combine(gameDirectory, "Game_Data");
        string basePath = Path.Combine(targetDirectory, "sharedassets0.assets");
        string optionalPath = Path.Combine(targetDirectory, "sharedassets1.assets");
        Directory.CreateDirectory(targetDirectory);
        File.WriteAllText(basePath, "original");
        File.WriteAllText(optionalPath, "original");
        TestManifest.WriteZip(
            zipPath,
            """
            {
              "targets": [
                {
                  "file": "sharedassets0.assets",
                  "patches": [
                    { "type": "Camera", "match": { "field of view": 90.0 }, "set": { "m_CullingMask.m_Bits": { "from": 3211820983, "to": 931037111 } } }
                  ]
                }
              ],
              "optional": [
                {
                  "name": "Bonus camera",
                  "description": "Patches a second file",
                  "targets": [
                    {
                      "file": "sharedassets1.assets",
                      "patches": [
                        { "type": "Camera", "match": { "field of view": 90.0 }, "set": { "m_CullingMask.m_Bits": { "from": 3211820983, "to": 931037111 } } }
                      ]
                    }
                  ]
                }
              ]
            }
            """);
        AssetsFieldInfo fieldTree = new("Camera", "Camera", null,
        [
            new AssetsFieldInfo("field of view", "float", "90.0", []),
            new AssetsFieldInfo("m_CullingMask", "BitField", null,
            [
                new AssetsFieldInfo("m_Bits", "UInt32", "3211820983", []),
            ]),
        ]);
        var assetsFileService = new StubAssetsFileService(
            new Dictionary<string, IReadOnlyList<AssetsInfo>>(StringComparer.OrdinalIgnoreCase)
            {
                [basePath] = [new AssetsInfo(4, 20, "Camera", 128)],
                [optionalPath] = [new AssetsInfo(5, 20, "Camera", 128)],
            },
            new Dictionary<(string AssetsFilePath, long PathId), AssetsFieldInfo>
            {
                [(basePath, 4)] = fieldTree,
                [(optionalPath, 5)] = fieldTree,
            });
        var workflow = CreateWorkflow(assetsFileService);

        try
        {
            InstallPreviewResult basePreview = workflow.Preview(
                new InstallPreviewRequest(zipPath, gameDirectory));

            OptionalGroupPreview group = Assert.Single(basePreview.OptionalGroups);
            Assert.Equal("Bonus camera", group.Name);
            Assert.Equal("Patches a second file", group.Description);
            Assert.Single(basePreview.Files);

            InstallPreviewResult mergedPreview = workflow.Preview(
                new InstallPreviewRequest(zipPath, gameDirectory)
                {
                    SelectedOptionalGroups = ["Bonus camera"],
                });

            Assert.Equal(2, mergedPreview.Files.Count);
            Assert.Contains(mergedPreview.Files, file => file.Target == "sharedassets1.assets");
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
    /// Verifies that installing selected optional content records the applied groups in the result and record.json.
    /// </summary>
    [Fact]
    public void Install_WhenOptionalGroupSelected_RecordsAppliedGroups()
    {
        string zipPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.zip");
        string gameDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string targetDirectory = Path.Combine(gameDirectory, "Game_Data");
        string backupDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(targetDirectory);
        File.WriteAllText(Path.Combine(targetDirectory, "sharedassets0.assets"), "original");
        File.WriteAllText(Path.Combine(targetDirectory, "sharedassets1.assets"), "original");
        WriteOptionalContentZip(zipPath);
        var assetsFileService = CreateCullingMaskCameraReader();
        var workflow = CreateWorkflow(assetsFileService);

        try
        {
            InstallModResult result = workflow.Install(
                new InstallModRequest(zipPath, gameDirectory, backupDirectory)
                {
                    SelectedOptionalGroups = ["bonus CAMERA"],
                });

            Assert.Equal(["Bonus camera"], result.OptionalGroups);
            Assert.Equal(2, result.Files.Count);
            string recordJson = ReadInstallRecordJson(backupDirectory);
            Assert.Contains("\"optionalGroups\"", recordJson);
            Assert.Contains("Bonus camera", recordJson);
        }
        finally
        {
            CleanUp(zipPath, gameDirectory, backupDirectory);
        }
    }

    /// <summary>
    /// Verifies that installing without optional selection omits the optionalGroups field from record.json.
    /// </summary>
    [Fact]
    public void Install_WhenNoOptionalSelected_OmitsOptionalGroupsFromRecord()
    {
        string zipPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.zip");
        string gameDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string targetDirectory = Path.Combine(gameDirectory, "Game_Data");
        string backupDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(targetDirectory);
        File.WriteAllText(Path.Combine(targetDirectory, "sharedassets0.assets"), "original");
        File.WriteAllText(Path.Combine(targetDirectory, "sharedassets1.assets"), "original");
        WriteOptionalContentZip(zipPath);
        var workflow = CreateWorkflow(CreateCullingMaskCameraReader());

        try
        {
            InstallModResult result = workflow.Install(
                new InstallModRequest(zipPath, gameDirectory, backupDirectory));

            Assert.Empty(result.OptionalGroups);
            Assert.Single(result.Files);
            Assert.DoesNotContain("optionalGroups", ReadInstallRecordJson(backupDirectory));
        }
        finally
        {
            CleanUp(zipPath, gameDirectory, backupDirectory);
        }
    }

    private static void WriteOptionalContentZip(string zipPath)
    {
        TestManifest.WriteZip(
            zipPath,
            """
            {
              "targets": [
                {
                  "file": "sharedassets0.assets",
                  "patches": [
                    { "type": "Camera", "match": { "field of view": 90.0 }, "set": { "m_CullingMask.m_Bits": { "from": 3211820983, "to": 931037111 } } }
                  ]
                }
              ],
              "optional": [
                {
                  "name": "Bonus camera",
                  "targets": [
                    {
                      "file": "sharedassets1.assets",
                      "patches": [
                        { "type": "Camera", "match": { "field of view": 90.0 }, "set": { "m_CullingMask.m_Bits": { "from": 3211820983, "to": 931037111 } } }
                      ]
                    }
                  ]
                }
              ]
            }
            """);
    }

    private static StubAssetsFileService CreateCullingMaskCameraReader()
    {
        return new StubAssetsFileService(
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
    }

    private static string ReadInstallRecordJson(string backupDirectory)
    {
        string recordPath = Directory
            .EnumerateFiles(backupDirectory, "record.json", SearchOption.AllDirectories)
            .Single();

        return File.ReadAllText(recordPath);
    }

    private static void CleanUp(string zipPath, string gameDirectory, string backupDirectory)
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

    /// <summary>
    /// Verifies that install throws a clear race condition error when a payload file already exists
    /// at the destination path during the copy phase.
    /// </summary>
    [Fact]
    public void Install_WhenPayloadFileExistsAtDestination_ThrowsRaceConditionError()
    {
        string zipPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.zip");
        string gameDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string targetDirectory = Path.Combine(gameDirectory, "Game_Data");
        string backupDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string payloadPath = Path.Combine(targetDirectory, "modassets.resource");
        Directory.CreateDirectory(targetDirectory);
        File.WriteAllText(Path.Combine(targetDirectory, "sharedassets4.assets"), "original");

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

        File.WriteAllText(payloadPath, "created by another process");

        var workflow = CreateWorkflow(assetsFileService);

        try
        {
            var exception = Assert.Throws<IOException>(() =>
                workflow.Install(new InstallModRequest(zipPath, gameDirectory, backupDirectory)));

            Assert.Contains("Payload file was created by another process", exception.Message);
            Assert.Contains("modassets.resource", exception.Message);
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

    private static InstallModWorkflow CreateWorkflow(StubAssetsFileService assetsFileService)
    {
        return CreateWorkflow(assetsFileService, new GameDirectoryResolver());
    }

    private static InstallModWorkflow CreateWorkflow(
        StubAssetsFileService assetsFileService,
        IEnumerable<string> steamRoots)
    {
        return CreateWorkflow(assetsFileService, new GameDirectoryResolver(steamRoots));
    }

    private static InstallModWorkflow CreateWorkflow(
        StubAssetsFileService assetsFileService,
        GameDirectoryResolver gameDirectoryResolver)
    {
        var assetQueryService = new AssetQueryService(assetsFileService);
        return new InstallModWorkflow(
            new PatchPlanBuilder(
                new FieldPatchPlanBuilder(assetQueryService),
                new ReplacementPlanBuilder(assetQueryService)),
            new PatchOutputWriter(assetsFileService),
            assetsFileService,
            new ModManifestReader(),
            gameDirectoryResolver,
            new TargetAssetResolver());
    }
}

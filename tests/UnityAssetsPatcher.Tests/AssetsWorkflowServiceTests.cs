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
        var service = CreateService(assetsFileService);

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
        var service = CreateService(assetsFileService);

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
        var service = CreateService(assetsFileService);

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
        var service = CreateService(new StubAssetsFileService(
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
        var service = CreateService(
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
        var service = CreateService(
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

        var service = CreateService(new StubAssetsFileService(
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
        var service = CreateService(assetsFileService);

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

    private static AssetsWorkflowService CreateService(StubAssetsFileService assetsFileService)
    {
        return new AssetsWorkflowService(assetsFileService, assetsFileService);
    }

    private static AssetsWorkflowService CreateService(
        StubAssetsFileService assetsFileService,
        GameDirectoryResolver gameDirectoryResolver)
    {
        return new AssetsWorkflowService(assetsFileService, assetsFileService, gameDirectoryResolver);
    }
}

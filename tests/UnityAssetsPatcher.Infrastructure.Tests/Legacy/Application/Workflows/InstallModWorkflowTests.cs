using System.IO.Compression;
using UnityAssetsPatcher.Application.Backups;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Installation;
using UnityAssetsPatcher.Application.Patching.Fields;
using UnityAssetsPatcher.Application.Workflows;
using UnityAssetsPatcher.Domain.Assets;
using UnityAssetsPatcher.Domain.Integrity;
using UnityAssetsPatcher.Tests.Support;
using Xunit;

namespace UnityAssetsPatcher.Tests.Application.Workflows;

public sealed class InstallModWorkflowTests
{
    [Fact]
    public void Install_WhenPreparedPreviewIsUnchanged_ReusesPreviewAnalysis()
    {
        using FieldPatchInstallScenario scenario = new();

        InstallPreviewResult preview = scenario.Workflow.Preview(
            new InstallRequest(scenario.ZipPath, scenario.GameDirectory));
        PreparedInstall preparedInstall = preview.PreparedInstall ??
                                          throw new InvalidOperationException(
                                              "Preview did not return a prepared install.");
        int previewReadCount = scenario.AssetsFileService.ReadFieldCount;

        _ = scenario.Workflow.Install(new InstallRequest(scenario.ZipPath, scenario.GameDirectory)
        {
            PreparedInstall = preparedInstall,
        });

        Assert.True(previewReadCount > 0);
        Assert.Equal(previewReadCount, scenario.AssetsFileService.ReadFieldCount);
        Assert.True(scenario.AssetsFileService.WasCalled);
    }

    [Fact]
    public void Install_WhenPreparedPreviewTargetChanges_RejectsPreparedPlan()
    {
        using FieldPatchInstallScenario scenario = new();

        InstallPreviewResult preview = scenario.Workflow.Preview(
            new InstallRequest(scenario.ZipPath, scenario.GameDirectory));
        PreparedInstall preparedInstall = preview.PreparedInstall ??
                                          throw new InvalidOperationException(
                                              "Preview did not return a prepared install.");
        File.AppendAllText(scenario.TargetPath, "changed");

        InstallPreparationStaleException exception = Assert.Throws<InstallPreparationStaleException>(() =>
            scenario.Workflow.Install(new InstallRequest(scenario.ZipPath, scenario.GameDirectory)
            {
                PreparedInstall = preparedInstall,
            }));

        Assert.Contains(scenario.TargetPath, exception.Message);
        Assert.False(scenario.AssetsFileService.WasCalled);
    }

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
            [new AssetInfo(4, "Camera")],
            new Dictionary<long, AssetField>
            {
                [4] = TestAssetField.Create("Camera", "Camera", null,
                [
                    TestAssetField.Create("field of view", "float", new AssetFieldValue.Float(90f), []),
                    TestAssetField.Create("m_CullingMask", "BitField", null,
                    [
                        TestAssetField.Create("m_Bits", "UInt32", new AssetFieldValue.UInt64(3211820983), []),
                    ]),
                ]),
            });
        var workflow = CreateWorkflow(assetsFileService, backupDirectory);

        try
        {
            InstallModResult result = workflow.Install(
                new InstallRequest(zipPath, gameDirectory));

            Assert.Equal("Test Mod", result.ModName);
            InstallChange file = SinglePatchChange(result);
            Assert.Equal("sharedassets0.assets", file.Name);
            Assert.Equal(targetPath, file.Path);
            Assert.StartsWith(backupDirectory, file.BackupPath, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(1, file.AssetCount);
            Assert.Equal(1, file.OperationCount);
            Assert.Equal(targetPath, assetsFileService.InputPath);
            Assert.Contains(BackupRepository.TransactionDirectoryName, assetsFileService.OutputPath);
            Assert.True(assetsFileService.CloseReadSessionsCountAtWrite >= 1);
            Assert.Equal("patched", File.ReadAllText(targetPath));
            Assert.True(File.Exists(file.BackupPath));
            string recordJson = ReadInstallRecordJson(backupDirectory);
            Assert.DoesNotContain("\"formatVersion\"", recordJson);
            Assert.Contains("\"repositoryId\"", recordJson);
            Assert.Contains("\"installedFile\"", recordJson);
            Assert.Contains("\"backupFile\"", recordJson);
            Assert.Contains("\"sha256\"", recordJson);
            Assert.Contains("\"length\"", recordJson);
            Assert.Contains("\"gameInstanceFingerprint\"", recordJson);
            Assert.Contains("\"installSequence\": 1", recordJson);
            Assert.DoesNotContain("\"gameDirectory\"", recordJson);
            Assert.DoesNotContain(targetPath, recordJson);
            InstallRecord storedRecord = Assert.Single(
                TestDependencies.CreateBackupRepository(
                    backupDirectory,
                    TestDependencies.FileSystemOperations).ListRecords()).Record;
            Assert.Equal(storedRecord.Id, result.InstallId);
            InstallRecordPatchedFile storedFile = Assert.Single(storedRecord.PatchedFiles);
            Assert.Equal(FileIntegrity.Create(targetPath), storedFile.InstalledFile);
            Assert.Equal(FileIntegrity.Create(file.BackupPath!), storedFile.BackupFile);
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
                      "schemaVersion": 1,
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
            [new AssetInfo(4, "Camera")],
            new Dictionary<long, AssetField>
            {
                [4] = TestAssetField.Create("Camera", "Camera", null,
                [
                    TestAssetField.Create("field of view", "float", new AssetFieldValue.Float(90f), []),
                    TestAssetField.Create("m_CullingMask", "BitField", null,
                    [
                        TestAssetField.Create("m_Bits", "UInt32", new AssetFieldValue.UInt64(3211820983), []),
                    ]),
                ]),
            });
        var workflow = CreateWorkflow(assetsFileService, backupDirectory);

        try
        {
            InstallModResult result = workflow.Install(
                new InstallRequest(zipPath, gameDirectory));

            InstallChange copiedFile = SinglePayloadChange(result);
            Assert.Equal("resources/modassets.resource", copiedFile.Name);
            Assert.Equal(copiedPath, copiedFile.Path);
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
                      "schemaVersion": 1,
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
            new Dictionary<string, IReadOnlyList<AssetInfo>>(StringComparer.OrdinalIgnoreCase)
            {
                [targetPath] =
                [
                    new AssetInfo(100, "AudioClip"),
                ],
                ["modassets.assets"] =
                [
                    new AssetInfo(200, "AudioClip"),
                ],
            },
            new Dictionary<(string AssetsFilePath, long PathId), AssetField>
            {
                [(targetPath, 100)] = CreateAudioClipFieldTree("Example Clip"),
                [("modassets.assets", 200)] = CreateAudioClipFieldTree("Example Clip"),
            });
        var workflow = CreateWorkflow(assetsFileService, backupDirectory);

        try
        {
            InstallPreviewResult preview = workflow.Preview(new InstallRequest(zipPath, gameDirectory));
            PreparedInstall preparedInstall = preview.PreparedInstall ??
                                              throw new InvalidOperationException(
                                                  "Preview did not return a prepared install.");
            InstallModResult result = workflow.Install(new InstallRequest(zipPath, gameDirectory)
            {
                PreparedInstall = preparedInstall,
            });

            InstallChange file = SinglePatchChange(result);
            Assert.Equal(1, file.AssetCount);
            Assert.Equal(1, file.OperationCount);
            AssetReplacement replacement = Assert.Single(assetsFileService.ReplacementPlan);
            Assert.Equal(200, replacement.SourcePathId);
            Assert.Equal(100, replacement.TargetPathId);
            Assert.Equal("modassets.assets", Path.GetFileName(replacement.SourceAssetsFilePath));
            Assert.StartsWith(Path.GetTempPath(), replacement.SourceAssetsFilePath,
                StringComparison.OrdinalIgnoreCase);
            Assert.Contains("UnityAssetsPatcher.", replacement.SourceAssetsFilePath);
            Assert.True(assetsFileService.ReplacementSourcesExistedAtWrite);
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
                      "schemaVersion": 1,
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
            new Dictionary<string, IReadOnlyList<AssetInfo>>(StringComparer.OrdinalIgnoreCase)
            {
                [targetPath] =
                [
                    new AssetInfo(100, "AudioClip"),
                ],
                ["modassets.assets"] =
                [
                    new AssetInfo(200, "AudioClip"),
                ],
            },
            new Dictionary<(string AssetsFilePath, long PathId), AssetField>
            {
                [(targetPath, 100)] = CreateAudioClipFieldTree("Example Clip"),
                [("modassets.assets", 200)] = CreateAudioClipFieldTree("Example Clip"),
            });
        var workflow = CreateWorkflow(assetsFileService, backupDirectory);

        try
        {
            InstallModResult result = workflow.Install(
                new InstallRequest(zipPath, gameDirectory));

            Assert.Single(PatchChanges(result.Changes));
            Assert.Single(PayloadChanges(result.Changes));
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
    /// Verifies that preview releases reads before deleting an extracted replacement source.
    /// </summary>
    [Fact]
    public void Preview_WhenZipHasReplacementSource_ClosesReadsBeforeDeletingExtractedSource()
    {
        string zipPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.zip");
        string gameDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string targetDirectory = Path.Combine(gameDirectory, "Game_Data");
        string targetPath = Path.Combine(targetDirectory, "sharedassets4.assets");
        Directory.CreateDirectory(targetDirectory);
        File.WriteAllText(targetPath, "original");

        using (ZipArchive archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            ZipArchiveEntry manifestEntry = archive.CreateEntry("manifest.json");
            using (StreamWriter writer = new(manifestEntry.Open()))
            {
                writer.Write(TestManifest.CreateJson(
                    """
                    {
                      "patches": [
                        {
                          "target": "sharedassets4.assets",
                          "type": "AudioClip",
                          "match": {
                            "m_Name": "Example Clip"
                          },
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
            new Dictionary<string, IReadOnlyList<AssetInfo>>(StringComparer.OrdinalIgnoreCase)
            {
                [targetPath] = [new AssetInfo(100, "AudioClip")],
                ["modassets.assets"] = [new AssetInfo(200, "AudioClip")],
            },
            new Dictionary<(string AssetsFilePath, long PathId), AssetField>
            {
                [(targetPath, 100)] = CreateAudioClipFieldTree("Example Clip"),
                [("modassets.assets", 200)] = CreateAudioClipFieldTree("Example Clip"),
            });
        var workflow = CreateWorkflow(assetsFileService);

        try
        {
            InstallPreviewResult result = workflow.Preview(new InstallRequest(zipPath, gameDirectory));

            Assert.Single(PatchChanges(result.Changes));
            Assert.True(assetsFileService.ReadFilesExistedAtClose);
            Assert.False(assetsFileService.WasCalled);
            Assert.Equal(1, assetsFileService.ScopeDisposeCount);
            Assert.Equal(1, assetsFileService.ReaderDisposeCount);
            Assert.Equal(0, assetsFileService.WriterCreateCount);
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
        string backupDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var assetsFileService = new StubAssetsFileService(
            [new AssetInfo(4, "Camera")],
            new Dictionary<long, AssetField>
            {
                [4] = TestAssetField.Create("Camera", "Camera", null,
                [
                    TestAssetField.Create("field of view", "float", new AssetFieldValue.Float(90f), []),
                    TestAssetField.Create("m_CullingMask", "BitField", null,
                    [
                        TestAssetField.Create("m_Bits", "UInt32", new AssetFieldValue.UInt64(3211820983), []),
                    ]),
                ]),
            });
        var workflow = CreateWorkflow(assetsFileService, backupDirectory);

        try
        {
            InstallPreviewResult result = workflow.Preview(
                new InstallRequest(zipPath, gameDirectory));

            Assert.Equal("Test Mod", result.ModName);
            InstallChange file = SinglePatchChange(result);
            Assert.Equal("sharedassets0.assets", file.Name);
            Assert.Equal(targetPath, file.Path);
            PatchPreviewAssetResult asset = Assert.Single(file.Preview!.Assets);
            Assert.Equal(4, asset.Asset.PathId);
            PatchPreviewOperationResult operation = Assert.Single(asset.Operations);
            Assert.True(operation.WillChange);
            Assert.Equal("m_CullingMask.m_Bits", operation.Path);
            Assert.Equal("3211820983", operation.OldValue);
            Assert.Equal("original", File.ReadAllText(targetPath));
            Assert.Equal(1, assetsFileService.ScopeCreateCount);
            Assert.Equal(1, assetsFileService.ScopeDisposeCount);
            Assert.Equal(1, assetsFileService.ReaderCreateCount);
            Assert.Equal(1, assetsFileService.ReaderDisposeCount);
            Assert.Equal(0, assetsFileService.WriterCreateCount);
            Assert.Equal(0, assetsFileService.WriterDisposeCount);
            Assert.False(Directory.Exists(backupDirectory));
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
                [new AssetInfo(4, "Camera")],
                new Dictionary<long, AssetField>
                {
                    [4] = TestAssetField.Create("Camera", "Camera", null,
                    [
                        TestAssetField.Create("field of view", "float", new AssetFieldValue.Float(90f), []),
                        TestAssetField.Create("m_CullingMask", "BitField", null,
                        [
                            TestAssetField.Create("m_Bits", "UInt32", new AssetFieldValue.UInt64(3211820983), []),
                        ]),
                    ]),
                }),
            [steamDirectory]);

        try
        {
            InstallPreviewResult result = workflow.Preview(
                new InstallRequest(zipPath, null));

            InstallChange file = SinglePatchChange(result);
            Assert.Equal(targetPath, file.Path);
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
        var assetsFileService = new StubAssetsFileService([]);
        var workflow = CreateWorkflow(assetsFileService, []);

        try
        {
            var exception = Assert.Throws<DirectoryNotFoundException>(() =>
                workflow.Preview(new InstallRequest(zipPath, null)));

            Assert.Contains("Game directory could not be resolved", exception.Message);
            Assert.Contains("Missing Game", exception.Message);
            Assert.Equal(0, assetsFileService.CloseReadSessionsCount);
            Assert.Equal(1, assetsFileService.ScopeDisposeCount);
            Assert.Equal(1, assetsFileService.ReaderCreateCount);
            Assert.Equal(1, assetsFileService.ReaderDisposeCount);
            Assert.Equal(0, assetsFileService.WriterCreateCount);
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
                      "schemaVersion": 1,
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
            [new AssetInfo(4, "Camera")],
            new Dictionary<long, AssetField>
            {
                [4] = TestAssetField.Create("Camera", "Camera", null,
                [
                    TestAssetField.Create("field of view", "float", new AssetFieldValue.Float(90f), []),
                    TestAssetField.Create("m_CullingMask", "BitField", null,
                    [
                        TestAssetField.Create("m_Bits", "UInt32", new AssetFieldValue.UInt64(3211820983), []),
                    ]),
                ]),
            }));

        try
        {
            InstallPreviewResult result = workflow.Preview(
                new InstallRequest(zipPath, gameDirectory));

            InstallChange copiedFile = SinglePayloadChange(result);
            Assert.Equal("resources/modassets.resource", copiedFile.Name);
            Assert.Equal(copiedPath, copiedFile.Path);
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
            [new AssetInfo(4, "Camera")],
            new Dictionary<long, AssetField>
            {
                [4] = TestAssetField.Create("Camera", "Camera", null,
                    [TestAssetField.Create("field of view", "float", new AssetFieldValue.Float(90f), [])]),
            });
        var workflow = CreateWorkflow(assetsFileService, backupDirectory);

        try
        {
            var exception = Assert.Throws<InvalidOperationException>(() =>
                workflow.Install(new InstallRequest(zipPath, gameDirectory)));

            Assert.Contains("matched multiple files", exception.Message);
            Assert.False(assetsFileService.WasCalled);
            Assert.Equal(0, assetsFileService.CloseReadSessionsCount);
            Assert.Equal(1, assetsFileService.ScopeDisposeCount);
            Assert.Equal(1, assetsFileService.ReaderCreateCount);
            Assert.Equal(1, assetsFileService.ReaderDisposeCount);
            Assert.Equal(0, assetsFileService.WriterCreateCount);
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
                      "schemaVersion": 1,
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
            new Dictionary<string, IReadOnlyList<AssetInfo>>(StringComparer.OrdinalIgnoreCase)
            {
                [targetPath] =
                [
                    new AssetInfo(100, "AudioClip"),
                ],
            },
            new Dictionary<(string AssetsFilePath, long PathId), AssetField>
            {
                [(targetPath, 100)] = CreateAudioClipFieldTree("Example Clip"),
            });
        var workflow = CreateWorkflow(assetsFileService, backupDirectory);

        try
        {
            Assert.ThrowsAny<Exception>(() =>
                workflow.Install(new InstallRequest(zipPath, gameDirectory)));

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
        AssetField fieldTree = TestAssetField.Create("Camera", "Camera", null,
        [
            TestAssetField.Create("field of view", "float", new AssetFieldValue.Float(90f), []),
            TestAssetField.Create("m_CullingMask", "BitField", null,
            [
                TestAssetField.Create("m_Bits", "UInt32", new AssetFieldValue.UInt64(3211820983), []),
            ]),
        ]);
        var assetsFileService = new StubAssetsFileService(
            new Dictionary<string, IReadOnlyList<AssetInfo>>(StringComparer.OrdinalIgnoreCase)
            {
                [basePath] = [new AssetInfo(4, "Camera")],
                [optionalPath] = [new AssetInfo(5, "Camera")],
            },
            new Dictionary<(string AssetsFilePath, long PathId), AssetField>
            {
                [(basePath, 4)] = fieldTree,
                [(optionalPath, 5)] = fieldTree,
            });
        var workflow = CreateWorkflow(assetsFileService);

        try
        {
            InstallPreviewResult basePreview = workflow.Preview(
                new InstallRequest(zipPath, gameDirectory));

            (string Name, string? Description) group = Assert.Single(basePreview.OptionalGroups);
            Assert.Equal("Bonus camera", group.Name);
            Assert.Equal("Patches a second file", group.Description);
            Assert.Single(PatchChanges(basePreview.Changes));

            InstallPreviewResult mergedPreview = workflow.Preview(
                new InstallRequest(zipPath, gameDirectory)
                {
                    SelectedOptionalGroups = ["Bonus camera"],
                });

            Assert.Equal(2, PatchChanges(mergedPreview.Changes).Count);
            Assert.Contains(PatchChanges(mergedPreview.Changes), file => file.Name == "sharedassets1.assets");
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
        var workflow = CreateWorkflow(assetsFileService, backupDirectory);

        try
        {
            InstallModResult result = workflow.Install(
                new InstallRequest(zipPath, gameDirectory)
                {
                    SelectedOptionalGroups = ["bonus CAMERA"],
                });

            Assert.Equal(["Bonus camera"], result.OptionalGroups);
            Assert.Equal(2, PatchChanges(result.Changes).Count);
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
        var workflow = CreateWorkflow(CreateCullingMaskCameraReader(), backupDirectory);

        try
        {
            InstallModResult result = workflow.Install(
                new InstallRequest(zipPath, gameDirectory));

            Assert.Empty(result.OptionalGroups);
            Assert.Single(PatchChanges(result.Changes));
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
            [new AssetInfo(4, "Camera")],
            new Dictionary<long, AssetField>
            {
                [4] = TestAssetField.Create("Camera", "Camera", null,
                [
                    TestAssetField.Create("field of view", "float", new AssetFieldValue.Float(90f), []),
                    TestAssetField.Create("m_CullingMask", "BitField", null,
                    [
                        TestAssetField.Create("m_Bits", "UInt32", new AssetFieldValue.UInt64(3211820983), []),
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
                      "schemaVersion": 1,
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
            [new AssetInfo(4, "Camera")],
            new Dictionary<long, AssetField>
            {
                [4] = TestAssetField.Create("Camera", "Camera", null,
                [
                    TestAssetField.Create("field of view", "float", new AssetFieldValue.Float(90f), []),
                    TestAssetField.Create("m_CullingMask", "BitField", null,
                    [
                        TestAssetField.Create("m_Bits", "UInt32", new AssetFieldValue.UInt64(3211820983), []),
                    ]),
                ]),
            });

        File.WriteAllText(payloadPath, "created by another process");

        var workflow = CreateWorkflow(assetsFileService, backupDirectory);

        try
        {
            var exception = Assert.Throws<BackupRecoveryException>(() =>
                workflow.Install(new InstallRequest(zipPath, gameDirectory)));

            Assert.Contains("automatic rollback was unsafe", exception.Message);
            Assert.Equal("original", File.ReadAllText(Path.Combine(targetDirectory, "sharedassets4.assets")));
            Assert.Empty(Directory.EnumerateFiles(
                Path.Combine(backupDirectory, BackupRepository.InstalledDirectoryName),
                "record.json",
                SearchOption.AllDirectories));
            Assert.True(Directory.Exists(Path.Combine(backupDirectory, BackupRepository.TransactionDirectoryName)));
            Assert.Equal(1, assetsFileService.CloseReadSessionsCount);
            Assert.Equal(1, assetsFileService.ScopeDisposeCount);
            Assert.Equal(1, assetsFileService.ReaderDisposeCount);
            Assert.Equal(1, assetsFileService.WriterDisposeCount);
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

    private static AssetField CreateAudioClipFieldTree(string name)
    {
        return TestAssetField.Create(
            "AudioClip",
            "AudioClip",
            null,
            [
                TestAssetField.Create("m_Name", "string", new AssetFieldValue.String(name), []),
            ]);
    }

    private static InstallChange SinglePatchChange(InstallModResult result)
    {
        return Assert.Single(PatchChanges(result.Changes));
    }

    private static InstallChange SinglePatchChange(InstallPreviewResult result)
    {
        return Assert.Single(PatchChanges(result.Changes));
    }

    private static InstallChange SinglePayloadChange(InstallModResult result)
    {
        return Assert.Single(PayloadChanges(result.Changes));
    }

    private static InstallChange SinglePayloadChange(InstallPreviewResult result)
    {
        return Assert.Single(PayloadChanges(result.Changes));
    }

    private static IReadOnlyList<InstallChange> PatchChanges(IReadOnlyList<InstallChange> changes)
    {
        return changes
            .Where(change => change.Kind == InstallChangeKind.Patch)
            .ToArray();
    }

    private static IReadOnlyList<InstallChange> PayloadChanges(IReadOnlyList<InstallChange> changes)
    {
        return changes
            .Where(change => change.Kind == InstallChangeKind.Payload)
            .ToArray();
    }

    private sealed class FieldPatchInstallScenario : IDisposable
    {
        public string ZipPath { get; }
        public string GameDirectory { get; }
        public string TargetPath { get; }
        public StubAssetsFileService AssetsFileService { get; }
        public InstallModWorkflow Workflow { get; }

        private string BackupDirectory { get; }

        public FieldPatchInstallScenario()
        {
            ZipPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.zip");
            GameDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            string targetDirectory = Path.Combine(GameDirectory, "Game_Data");
            TargetPath = Path.Combine(targetDirectory, "sharedassets0.assets");
            BackupDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(targetDirectory);
            File.WriteAllText(TargetPath, "original");
            TestManifest.WriteZip(
                ZipPath,
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
            AssetsFileService = new StubAssetsFileService(
                [new AssetInfo(4, "Camera")],
                new Dictionary<long, AssetField>
                {
                    [4] = TestAssetField.Create("Camera", "Camera", null,
                    [
                        TestAssetField.Create("field of view", "float", new AssetFieldValue.Float(90f), []),
                        TestAssetField.Create("m_CullingMask", "BitField", null,
                        [
                            TestAssetField.Create("m_Bits", "UInt32", new AssetFieldValue.UInt64(3211820983), []),
                        ]),
                    ]),
                });
            Workflow = CreateWorkflow(AssetsFileService, BackupDirectory);
        }

        public void Dispose()
        {
            File.Delete(ZipPath);

            if (Directory.Exists(GameDirectory))
            {
                Directory.Delete(GameDirectory, true);
            }

            if (Directory.Exists(BackupDirectory))
            {
                Directory.Delete(BackupDirectory, true);
            }
        }
    }

    private static InstallModWorkflow CreateWorkflow(StubAssetsFileService assetsFileService)
    {
        return CreateWorkflow(
            assetsFileService,
            TestDependencies.CreateGameDirectoryResolver(),
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
    }

    private static InstallModWorkflow CreateWorkflow(
        StubAssetsFileService assetsFileService,
        string backupDirectory)
    {
        return CreateWorkflow(assetsFileService, TestDependencies.CreateGameDirectoryResolver(), backupDirectory);
    }

    private static InstallModWorkflow CreateWorkflow(
        StubAssetsFileService assetsFileService,
        IEnumerable<string> steamRoots)
    {
        return CreateWorkflow(
            assetsFileService,
            TestDependencies.CreateGameDirectoryResolver(steamRoots),
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
    }

    private static InstallModWorkflow CreateWorkflow(
        StubAssetsFileService assetsFileService,
        GameDirectoryResolver gameDirectoryResolver,
        string? backupDirectory = null)
    {
        var archiveService = TestDependencies.CreateModPackageArchiveService();
        var planBuilder = new InstallPlanBuilder(
            new TargetAssetResolver(TestDependencies.FileSystemOperations),
            gameDirectoryResolver,
            [new SetFieldPatchOperationHandler(), new AddFieldPatchOperationHandler()]);
        var backupStore = TestDependencies.CreateBackupRepository(backupDirectory ??
                                                                  Path.Combine(Path.GetTempPath(),
                                                                      Guid.NewGuid().ToString("N")),
            TestDependencies.FileSystemOperations);
        var executor = new InstallExecutor(
            backupStore,
            TestDependencies.FileSystemOperations);

        return new InstallModWorkflow(
            archiveService,
            planBuilder,
            executor,
            backupStore,
            assetsFileService,
            TestDependencies.FileSystemOperations);
    }
}

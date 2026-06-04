using System.IO.Compression;
using Xunit;
using UnityAssetsPatcher.Application.Manifests;
using UnityAssetsPatcher.Application.Contracts;

namespace UnityAssetsPatcher.Tests;

public sealed class ModManifestLoaderTests
{
    /// <summary>
    /// Verifies that a manifest can carry mod metadata while using target groups for patch behavior.
    /// </summary>
    [Fact]
    public void Load_WhenManifestHasMetadataAndTargets_ReturnsMetadataAndPatches()
    {
        string configPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        File.WriteAllText(
            configPath,
            """
            {
              "name": "Camera Tweak",
              "author": "CnBarrier",
              "version": "1.0.0",
              "description": "Adjusts camera settings.",
              "copyFiles": [
                {
                  "source": "resources/modassets.resource"
                }
              ],
              "targets": [
                {
                  "file": "sharedassets0.assets",
                  "patches": [
                    {
                      "type": "Camera",
                      "match": {
                        "field of view": 90.0
                      },
                      "set": {
                        "field of view": {
                          "from": 90.0,
                          "to": 75.0
                        }
                      },
                      "add": {
                        "m_ValidKeywords.Array": ["_EMISSION"]
                      }
                    }
                  ]
                },
                {
                  "file": "sharedassets4.assets",
                  "patches": [
                    {
                      "type": "AudioClip",
                      "match": {
                        "m_Name": "Incense burn 1"
                      },
                      "replaceAsset": {
                        "fromFile": "resources/modassets.assets",
                        "matchField": "m_Name"
                      }
                    }
                  ]
                }
              ]
            }
            """);

        try
        {
            ModManifest config = ModManifestLoader.Load(configPath);

            Assert.Equal("Camera Tweak", config.Name);
            Assert.Equal("CnBarrier", config.Author);
            Assert.Equal("1.0.0", config.Version);
            Assert.Equal("Adjusts camera settings.", config.Description);
            ManifestFile file = Assert.Single(config.Files);
            Assert.Equal("resources/modassets.resource", file.Source);
            Assert.Equal(2, config.Patches.Count);

            ManifestPatch fieldPatch = config.Patches[0];
            Assert.Equal("sharedassets0.assets", fieldPatch.AssetsFileName);
            Assert.Equal("Camera", fieldPatch.AssetTypeName);
            Assert.Equal(90.0, Assert.Single(fieldPatch.IncludeGroups).Single().Value.GetDouble());
            ManifestSetOperation setOperation = Assert.Single(fieldPatch.SetOperations!);
            Assert.Equal("field of view", setOperation.FieldPath);
            Assert.Equal(90.0, setOperation.From.GetDouble());
            Assert.Equal(75.0, setOperation.To.GetDouble());
            ManifestAddOperation addOperation = Assert.Single(fieldPatch.AddOperations!);
            Assert.Equal("m_ValidKeywords.Array", addOperation.FieldPath);
            Assert.Equal("_EMISSION", addOperation.Value.EnumerateArray().Single().GetString());

            ManifestPatch replacementPatch = config.Patches[1];
            Assert.Equal("sharedassets4.assets", replacementPatch.AssetsFileName);
            Assert.Equal("AudioClip", replacementPatch.AssetTypeName);
            Assert.NotNull(replacementPatch.ReplaceFrom);
            Assert.Equal("resources/modassets.assets", replacementPatch.ReplaceFrom.AssetsFilePath);
            Assert.Equal("m_Name", replacementPatch.ReplaceFrom.MatchFieldPath);
        }
        finally
        {
            File.Delete(configPath);
        }
    }

    /// <summary>
    /// Verifies that a manifest missing required mod metadata returns a clear error.
    /// </summary>
    [Fact]
    public void Load_WhenManifestIsMissingRequiredMetadata_ThrowsClearError()
    {
        string configPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        File.WriteAllText(
            configPath,
            """
            {
              "name": "Camera Tweak",
              "author": "CnBarrier",
              "target": "sharedassets0.assets",
              "type": "Camera",
              "include": [
                {
                  "field of view": 90.0
                }
              ]
            }
            """);

        try
        {
            var exception = Assert.Throws<InvalidOperationException>(() => ModManifestLoader.Load(configPath));

            Assert.Contains("version", exception.Message);
        }
        finally
        {
            File.Delete(configPath);
        }
    }

    /// <summary>
    /// Verifies that required mod metadata must be non-empty strings.
    /// </summary>
    [Theory]
    [InlineData("name", "\"\"", "name")]
    [InlineData("author", "42", "author")]
    [InlineData("version", "true", "version")]
    public void Load_WhenRequiredMetadataIsEmptyOrWrongType_ThrowsClearError(
        string propertyName,
        string propertyValue,
        string expectedMessage)
    {
        string configPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        string nameValue = propertyName == "name" ? propertyValue : "\"Camera Tweak\"";
        string authorValue = propertyName == "author" ? propertyValue : "\"CnBarrier\"";
        string versionValue = propertyName == "version" ? propertyValue : "\"1.0.0\"";
        File.WriteAllText(
            configPath,
            $$"""
              {
                "name": {{nameValue}},
                "author": {{authorValue}},
                "version": {{versionValue}},
                "target": "sharedassets0.assets",
                "type": "Camera",
                "include": [
                  {
                    "field of view": 90.0
                  }
                ]
              }
              """);

        try
        {
            var exception = Assert.Throws<InvalidOperationException>(() => ModManifestLoader.Load(configPath));

            Assert.Contains(expectedMessage, exception.Message);
        }
        finally
        {
            File.Delete(configPath);
        }
    }

    /// <summary>
    /// Verifies that an optional description must be a string when present.
    /// </summary>
    [Fact]
    public void Load_WhenDescriptionIsNotString_ThrowsClearError()
    {
        string configPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        File.WriteAllText(
            configPath,
            """
            {
              "name": "Camera Tweak",
              "author": "CnBarrier",
              "version": "1.0.0",
              "description": 42,
              "target": "sharedassets0.assets",
              "type": "Camera",
              "include": [
                {
                  "field of view": 90.0
                }
              ]
            }
            """);

        try
        {
            var exception = Assert.Throws<InvalidOperationException>(() => ModManifestLoader.Load(configPath));

            Assert.Contains("description", exception.Message);
        }
        finally
        {
            File.Delete(configPath);
        }
    }

    /// <summary>
    /// Verifies that a manifest patch target must declare the assets file name to locate.
    /// </summary>
    [Fact]
    public void Load_WhenPatchTargetHasTarget_ReturnsTargetFileName()
    {
        string configPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        TestManifest.Write(
            configPath,
            """
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
            """);

        try
        {
            ModManifest config = ModManifestLoader.Load(configPath);

            ManifestPatch patch = Assert.Single(config.Patches);
            Assert.Equal("sharedassets0.assets", patch.AssetsFileName);
            ManifestSetOperation operation = Assert.Single(patch.SetOperations!);
            Assert.Equal("m_CullingMask.m_Bits", operation.FieldPath);
        }
        finally
        {
            File.Delete(configPath);
        }
    }

    /// <summary>
    /// Verifies that an object-level replacement can declare the source assets file and match field.
    /// </summary>
    [Fact]
    public void Load_WhenPatchTargetHasReplaceFrom_ReturnsReplacementSource()
    {
        string configPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        TestManifest.Write(
            configPath,
            """
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
            """);

        try
        {
            ModManifest config = ModManifestLoader.Load(configPath);

            ManifestPatch patch = Assert.Single(config.Patches);
            Assert.NotNull(patch.ReplaceFrom);
            Assert.Equal("resources/modassets.assets", patch.ReplaceFrom.AssetsFilePath);
            Assert.Equal("m_Name", patch.ReplaceFrom.MatchFieldPath);
        }
        finally
        {
            File.Delete(configPath);
        }
    }

    /// <summary>
    /// Verifies that a manifest can declare zip payload files to copy during install.
    /// </summary>
    [Fact]
    public void Load_WhenManifestHasFiles_ReturnsPayloadFiles()
    {
        string configPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        TestManifest.Write(
            configPath,
            """
            {
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
            """);

        try
        {
            ModManifest config = ModManifestLoader.Load(configPath);

            ManifestFile file = Assert.Single(config.Files);
            Assert.Equal("resources/modassets.resource", file.Source);
        }
        finally
        {
            File.Delete(configPath);
        }
    }

    /// <summary>
    /// Verifies that payload file sources must stay inside the mod zip namespace.
    /// </summary>
    [Theory]
    [InlineData("../modassets.resource")]
    [InlineData("/modassets.resource")]
    [InlineData("resources/../modassets.resource")]
    public void Load_WhenFileSourceEscapesZipNamespace_ThrowsClearError(string source)
    {
        string configPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        TestManifest.Write(
            configPath,
            $$"""
              {
                "files": [
                  {
                    "source": "{{source}}"
                  }
                ],
                "target": "sharedassets4.assets",
                "type": "AudioClip",
                "include": [
                  {
                    "m_Name": "Incense burn 1"
                  }
                ]
              }
              """);

        try
        {
            var exception = Assert.Throws<InvalidOperationException>(() => ModManifestLoader.Load(configPath));

            Assert.Contains("source", exception.Message);
        }
        finally
        {
            File.Delete(configPath);
        }
    }

    /// <summary>
    /// Verifies that set[].path is rejected to avoid conflicting with target file-location semantics.
    /// </summary>
    [Fact]
    public void Load_WhenSetUsesPath_ThrowsMigrationError()
    {
        string configPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        TestManifest.Write(
            configPath,
            """
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
                  "path": "field of view",
                  "from": 90.0,
                  "to": 75.0
                }
              ]
            }
            """);

        try
        {
            var exception = Assert.Throws<InvalidOperationException>(() => ModManifestLoader.Load(configPath));

            Assert.Contains("field", exception.Message);
            Assert.Contains("path", exception.Message);
        }
        finally
        {
            File.Delete(configPath);
        }
    }

    /// <summary>
    /// Verifies that target must be only a file name and cannot contain directory segments.
    /// </summary>
    [Fact]
    public void Load_WhenTargetContainsDirectorySeparator_ThrowsClearError()
    {
        string configPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        TestManifest.Write(
            configPath,
            """
            {
              "target": "Game_Data/sharedassets0.assets",
              "type": "Camera",
              "include": [
                {
                  "field of view": 90.0
                }
              ]
            }
            """);

        try
        {
            var exception = Assert.Throws<InvalidOperationException>(() => ModManifestLoader.Load(configPath));

            Assert.Contains("target", exception.Message);
            Assert.Contains("file name", exception.Message);
        }
        finally
        {
            File.Delete(configPath);
        }
    }

    /// <summary>
    /// Verifies that the only manifest.json in a nested zip directory is read automatically.
    /// </summary>
    [Fact]
    public void Load_WhenZipHasSingleNestedManifest_ReturnsConfig()
    {
        string zipPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.zip");

        try
        {
            using (ZipArchive archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                ZipArchiveEntry entry = archive.CreateEntry("CameraTweak/manifest.json");
                using StreamWriter writer = new(entry.Open());
                writer.Write(
                    """
                    {
                      "name": "Camera Tweak",
                      "author": "CnBarrier",
                      "version": "1.0.0",
                      "patches": [
                        {
                          "target": "sharedassets0.assets",
                          "type": "Camera",
                          "include": [
                            {
                              "field of view": 90.0
                            }
                          ]
                        }
                      ]
                    }
                    """);
            }

            ModManifest config = ModManifestLoader.Load(zipPath);

            Assert.Equal("Camera Tweak", config.Name);
            Assert.Equal("sharedassets0.assets", Assert.Single(config.Patches).AssetsFileName);
        }
        finally
        {
            File.Delete(zipPath);
        }
    }

    /// <summary>
    /// Verifies that a zip cannot contain multiple manifest.json entries, preventing ambiguous selection.
    /// </summary>
    [Fact]
    public void Load_WhenZipHasMultipleManifests_ThrowsClearError()
    {
        string zipPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.zip");

        try
        {
            using (ZipArchive archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                archive.CreateEntry("manifest.json");
                archive.CreateEntry("Nested/manifest.json");
            }

            var exception = Assert.Throws<InvalidOperationException>(() => ModManifestLoader.Load(zipPath));

            Assert.Contains("exactly one manifest.json", exception.Message);
        }
        finally
        {
            File.Delete(zipPath);
        }
    }

    /// <summary>
    /// Verifies that a zip missing manifest.json returns a clear error.
    /// </summary>
    [Fact]
    public void Load_WhenZipHasNoManifest_ThrowsClearError()
    {
        string zipPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.zip");

        try
        {
            using (ZipArchive archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                archive.CreateEntry("readme.txt");
            }

            var exception = Assert.Throws<InvalidOperationException>(() => ModManifestLoader.Load(zipPath));

            Assert.Contains("exactly one manifest.json", exception.Message);
        }
        finally
        {
            File.Delete(zipPath);
        }
    }
}

using System.IO.Compression;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Manifests;
using UnityAssetsPatcher.Tests.Support;
using Xunit;

namespace UnityAssetsPatcher.Tests.Application;

public sealed class ModManifestReaderTests
{
    /// <summary>
    /// Verifies that the manifest reader can load a manifest from a file path.
    /// </summary>
    [Fact]
    public void Load_WhenPathProvided_ReturnsConfig()
    {
        string configPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        File.WriteAllText(
            configPath,
            """
            {
              "name": "Example Mod",
              "author": "Author Name",
              "version": "1.0.0",
              "targets": [
                {
                  "file": "sharedassets0.assets",
                  "patches": [
                    {
                      "type": "Camera",
                      "match": {
                        "field of view": 90.0
                      }
                    }
                  ]
                }
              ]
            }
            """);

        try
        {
            var reader = new ModManifestReader();

            ModManifest config = reader.Load(configPath);

            Assert.Equal("Example Mod", config.Name);
            Assert.Equal("sharedassets0.assets", Assert.Single(config.Patches).AssetsFileName);
        }
        finally
        {
            File.Delete(configPath);
        }
    }

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
              "name": "Example Mod",
              "author": "Author Name",
              "version": "1.0.0",
              "description": "Example description.",
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
                        "m_Name": "Example Clip"
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
            ModManifest config = new ModManifestReader().Load(configPath);

            Assert.Equal("Example Mod", config.Name);
            Assert.Equal("Author Name", config.Author);
            Assert.Equal("1.0.0", config.Version);
            Assert.Equal("Example description.", config.Description);
            Assert.Null(config.Game);
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
    /// Verifies that a manifest can declare the target game as a simple top-level string.
    /// </summary>
    [Fact]
    public void Load_WhenManifestHasGame_ReturnsGame()
    {
        string configPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        TestManifest.Write(
            configPath,
            """
            {
              "game": "Example Game",
              "targets": [
                {
                  "file": "sharedassets0.assets",
                  "patches": [
                    {
                      "type": "Camera",
                      "match": {
                        "field of view": 90.0
                      }
                    }
                  ]
                }
              ]
            }
            """);

        try
        {
            ModManifest config = new ModManifestReader().Load(configPath);

            Assert.Equal("Example Game", config.Game);
        }
        finally
        {
            File.Delete(configPath);
        }
    }

    /// <summary>
    /// Verifies that the optional game property must be a non-empty string when present.
    /// </summary>
    [Theory]
    [InlineData("\"\"")]
    [InlineData("42")]
    public void Load_WhenGameIsEmptyOrWrongType_ThrowsClearError(string gameValue)
    {
        string configPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        TestManifest.Write(
            configPath,
            $$"""
              {
                "game": {{gameValue}},
                "targets": [
                  {
                    "file": "sharedassets0.assets",
                    "patches": [
                      {
                        "type": "Camera",
                        "match": {
                          "field of view": 90.0
                        }
                      }
                    ]
                  }
                ]
              }
              """);

        try
        {
            var exception = Assert.Throws<InvalidOperationException>(() => new ModManifestReader().Load(configPath));

            Assert.Contains("game", exception.Message);
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
              "name": "Example Mod",
              "author": "Author Name",
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
            var exception = Assert.Throws<InvalidOperationException>(() => new ModManifestReader().Load(configPath));

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
        string nameValue = propertyName == "name" ? propertyValue : "\"Example Mod\"";
        string authorValue = propertyName == "author" ? propertyValue : "\"Author Name\"";
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
            var exception = Assert.Throws<InvalidOperationException>(() => new ModManifestReader().Load(configPath));

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
              "name": "Example Mod",
              "author": "Author Name",
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
            var exception = Assert.Throws<InvalidOperationException>(() => new ModManifestReader().Load(configPath));

            Assert.Contains("description", exception.Message);
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
                "copyFiles": [
                  {
                    "source": "{{source}}"
                  }
                ],
                "targets": [
                  {
                    "file": "sharedassets4.assets",
                    "patches": [
                      {
                        "type": "AudioClip",
                        "match": {
                          "m_Name": "Example Clip"
                        }
                      }
                    ]
                  }
                ]
              }
              """);

        try
        {
            var exception = Assert.Throws<InvalidOperationException>(() => new ModManifestReader().Load(configPath));

            Assert.Contains("source", exception.Message);
        }
        finally
        {
            File.Delete(configPath);
        }
    }

    /// <summary>
    /// Verifies that replacement sources must stay inside the mod zip namespace.
    /// </summary>
    [Theory]
    [InlineData("C:/Users/victim/sensitive.assets")]
    [InlineData("../modassets.assets")]
    [InlineData("/modassets.assets")]
    [InlineData("resources/../modassets.assets")]
    [InlineData("resources//modassets.assets")]
    public void Load_WhenReplaceAssetFromFileEscapesZipNamespace_ThrowsClearError(string fromFile)
    {
        string configPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        TestManifest.Write(
            configPath,
            $$"""
              {
                "targets": [
                  {
                    "file": "sharedassets4.assets",
                    "patches": [
                      {
                        "type": "AudioClip",
                        "match": {
                          "m_Name": "Example Clip"
                        },
                        "replaceAsset": {
                          "fromFile": "{{fromFile}}",
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
            var exception = Assert.Throws<InvalidOperationException>(() => new ModManifestReader().Load(configPath));

            Assert.Contains("fromFile", exception.Message);
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
              "targets": [
                {
                  "file": "Game_Data/sharedassets0.assets",
                  "patches": [
                    {
                      "type": "Camera",
                      "match": {
                        "field of view": 90.0
                      }
                    }
                  ]
                }
              ]
            }
            """);

        try
        {
            var exception = Assert.Throws<InvalidOperationException>(() => new ModManifestReader().Load(configPath));

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
                      "name": "Example Mod",
                      "author": "Author Name",
                      "version": "1.0.0",
                      "targets": [
                        {
                          "file": "sharedassets0.assets",
                          "patches": [
                            {
                              "type": "Camera",
                              "match": {
                                "field of view": 90.0
                              }
                            }
                          ]
                        }
                      ]
                    }
                    """);
            }

            ModManifest config = new ModManifestReader().Load(zipPath);

            Assert.Equal("Example Mod", config.Name);
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

            var exception = Assert.Throws<InvalidOperationException>(() => new ModManifestReader().Load(zipPath));

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

            var exception = Assert.Throws<InvalidOperationException>(() => new ModManifestReader().Load(zipPath));

            Assert.Contains("exactly one manifest.json", exception.Message);
        }
        finally
        {
            File.Delete(zipPath);
        }
    }
}

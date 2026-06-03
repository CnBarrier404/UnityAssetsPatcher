using System.IO.Compression;
using UnityAssetsPatcher.Core;
using Xunit;

namespace UnityAssetsPatcher.Tests;

public sealed class AssetQueryConfigLoaderTests
{
    /// <summary>
    /// 验证 manifest 可以携带 Mod 元信息，并继续使用 patches 描述现有 patch 功能。
    /// </summary>
    [Fact]
    public void Load_WhenManifestHasMetadataAndPatches_ReturnsMetadataAndTargets()
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

        try
        {
            AssetQueryConfig config = AssetQueryConfigLoader.Load(configPath);

            Assert.Equal("Camera Tweak", config.Name);
            Assert.Equal("CnBarrier", config.Author);
            Assert.Equal("1.0.0", config.Version);
            Assert.Equal("Adjusts camera settings.", config.Description);
            AssetPatchTarget target = Assert.Single(config.Targets);
            Assert.Equal("sharedassets0.assets", target.Target);
            Assert.Equal("Camera", target.Type);
        }
        finally
        {
            File.Delete(configPath);
        }
    }

    /// <summary>
    /// 验证 manifest 缺少必填 Mod 元信息时会给出明确错误。
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
            var exception = Assert.Throws<InvalidOperationException>(() => AssetQueryConfigLoader.Load(configPath));

            Assert.Contains("version", exception.Message);
        }
        finally
        {
            File.Delete(configPath);
        }
    }

    /// <summary>
    /// 验证必填 Mod 元信息必须是非空字符串。
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
            var exception = Assert.Throws<InvalidOperationException>(() => AssetQueryConfigLoader.Load(configPath));

            Assert.Contains(expectedMessage, exception.Message);
        }
        finally
        {
            File.Delete(configPath);
        }
    }

    /// <summary>
    /// 验证可选 description 存在时必须是字符串。
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
            var exception = Assert.Throws<InvalidOperationException>(() => AssetQueryConfigLoader.Load(configPath));

            Assert.Contains("description", exception.Message);
        }
        finally
        {
            File.Delete(configPath);
        }
    }

    /// <summary>
    /// 验证 manifest 的 patch target 必须声明要自动查找的目标 assets 文件名。
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
            AssetQueryConfig config = AssetQueryConfigLoader.Load(configPath);

            AssetPatchTarget target = Assert.Single(config.Targets);
            Assert.Equal("sharedassets0.assets", target.Target);
            PatchSetOperation operation = Assert.Single(target.SetOperations!);
            Assert.Equal("m_CullingMask.m_Bits", operation.Path);
        }
        finally
        {
            File.Delete(configPath);
        }
    }

    /// <summary>
    /// 验证 set[].path 不再被接受，避免和 target 文件定位语义冲突。
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
            var exception = Assert.Throws<InvalidOperationException>(() => AssetQueryConfigLoader.Load(configPath));

            Assert.Contains("field", exception.Message);
            Assert.Contains("path", exception.Message);
        }
        finally
        {
            File.Delete(configPath);
        }
    }

    /// <summary>
    /// 验证 target 只能是文件名，不能包含目录片段。
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
            var exception = Assert.Throws<InvalidOperationException>(() => AssetQueryConfigLoader.Load(configPath));

            Assert.Contains("target", exception.Message);
            Assert.Contains("file name", exception.Message);
        }
        finally
        {
            File.Delete(configPath);
        }
    }

    /// <summary>
    /// 验证 zip 中嵌套目录内的唯一 manifest.json 会被自动读取。
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

            AssetQueryConfig config = AssetQueryConfigLoader.Load(zipPath);

            Assert.Equal("Camera Tweak", config.Name);
            Assert.Equal("sharedassets0.assets", Assert.Single(config.Targets).Target);
        }
        finally
        {
            File.Delete(zipPath);
        }
    }

    /// <summary>
    /// 验证 zip 中不能出现多个 manifest.json，避免工具自动选错。
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

            var exception = Assert.Throws<InvalidOperationException>(() => AssetQueryConfigLoader.Load(zipPath));

            Assert.Contains("exactly one manifest.json", exception.Message);
        }
        finally
        {
            File.Delete(zipPath);
        }
    }

    /// <summary>
    /// 验证 zip 缺少 manifest.json 时给出明确错误。
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

            var exception = Assert.Throws<InvalidOperationException>(() => AssetQueryConfigLoader.Load(zipPath));

            Assert.Contains("exactly one manifest.json", exception.Message);
        }
        finally
        {
            File.Delete(zipPath);
        }
    }
}

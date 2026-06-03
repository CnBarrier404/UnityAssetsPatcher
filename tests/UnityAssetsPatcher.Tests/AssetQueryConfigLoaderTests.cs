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
}

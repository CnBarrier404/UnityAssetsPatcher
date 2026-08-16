using UnityAssetsPatcher.Application.Installation;
using UnityAssetsPatcher.Application.Tests.Mods;
using UnityAssetsPatcher.Application.Mods;
using Xunit;

namespace UnityAssetsPatcher.Application.Tests.Installation;

public sealed class InstallPlanBuilderTests
{
    [Fact]
    public void PlanPayloadFiles_WhenPayloadTargetMatchesAssetsTarget_ThrowsInvalidDataException()
    {
        ModManifest manifest = ManifestTestHost.FromText(
            """
            {
              "$schema": "https://uap.cnbarrier.com/schema-v1.json",
              "name": "Conflicting Targets",
              "author": "Test Author",
              "version": "1.0.0",
              "copyFiles": [
                { "source": "payload/sharedassets0.assets" }
              ],
              "targets": [
                {
                  "file": "sharedassets0.assets",
                  "patches": [
                    {
                      "type": "TextAsset",
                      "match": { "m_Name": "Text" },
                      "set": {
                        "m_Name": { "from": "Text", "to": "Patched" }
                      }
                    }
                  ]
                }
              ]
            }
            """).Read();
        string assetsPath = Path.GetFullPath(Path.Combine(
            Path.GetTempPath(),
            "install-plan",
            "sharedassets0.assets"));
        TargetAssetSet targets = new(
        [
            new TargetAsset("sharedassets0.assets", assetsPath, manifest.Patches)
        ]);

        var exception =
            Assert.Throws<InvalidDataException>(() => InstallPlanBuilder.PlanPayloadFiles(manifest, targets));

        Assert.Contains(assetsPath, exception.Message);
    }
}

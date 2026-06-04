using UnityAssetsPatcher.Core.Assets;
using Xunit;

namespace UnityAssetsPatcher.Tests;

public sealed class AssetFieldMatcherTests
{
    [Fact]
    public void FindField_WhenPathUsesChildValueSelector_ReturnsSelectedDescendant()
    {
        AssetsFieldInfo fieldTree = CreateMaterialFieldTree(pathId: "8842");

        AssetsFieldInfo? field = AssetFieldMatcher.FindField(
            fieldTree,
            "m_SavedProperties.m_TexEnvs.Array.data[first=_EmissionMap].second.m_Texture.m_PathID");

        Assert.NotNull(field);
        Assert.Equal("m_PathID", field.Name);
        Assert.Equal("8842", field.Value);
    }

    private static AssetsFieldInfo CreateMaterialFieldTree(string pathId)
    {
        return new AssetsFieldInfo(
            "Material",
            "Material",
            null,
            [
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

using System.Text.Json;
using UnityAssetsPatcher.Application.Contracts;

namespace UnityAssetsPatcher.Application.Manifests;

public interface IModManifestLoader
{
    public ModManifest Load(string configPath);

    public ModManifest Load(JsonElement manifestElement)
    {
        return ModManifestLoader.Parse(manifestElement);
    }
}

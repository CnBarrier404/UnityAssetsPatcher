using UnityAssetsPatcher.Application.Contracts;

namespace UnityAssetsPatcher.Application.Manifests;

public interface IModManifestLoader
{
    public ModManifest Load(string configPath);
}

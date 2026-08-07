namespace UnityAssetsPatcher.Application.Manifests;

public interface IModManifestService
{
    public Task<ModManifest> ReadManifestAsync(string sourcePath, CancellationToken cancellationToken = default);
}

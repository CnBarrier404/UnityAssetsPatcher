namespace UnityAssetsPatcher.Application.Mods;

public interface IModPackageReader
{
    public Task<byte[]> ReadManifestAsync(string packagePath, CancellationToken cancellationToken = default);

    public IModPackageSession Open(string packagePath);
}

namespace UnityAssetsPatcher.Application.Mods;

public interface IModPackageSession : IDisposable
{
    public byte[] ReadManifest();

    public Task<byte[]> ReadManifestAsync(CancellationToken cancellationToken = default);

    public long CopyEntryToNewFile(
        string source,
        string destinationPath,
        CancellationToken cancellationToken = default);
}

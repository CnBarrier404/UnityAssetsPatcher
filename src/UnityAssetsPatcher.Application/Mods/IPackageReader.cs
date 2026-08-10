namespace UnityAssetsPatcher.Application.Mods;

public sealed record PackageContent(byte[] Manifest, IReadOnlyDictionary<string, string> EntryPaths);

public interface IPackageReader
{
    public PackageContent Read(
        string packagePath,
        string extractionDirectory,
        CancellationToken cancellationToken = default);

    public Task<byte[]> ReadManifestAsync(string packagePath, CancellationToken cancellationToken = default);
}

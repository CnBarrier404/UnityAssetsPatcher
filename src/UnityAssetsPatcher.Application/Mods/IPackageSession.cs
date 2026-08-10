using UnityAssetsPatcher.Application.Operations;

namespace UnityAssetsPatcher.Application.Mods;

public interface IPackageSession : IDisposable
{
    public OperationResult<byte[]> ReadManifest();

    public Task<OperationResult<byte[]>> ReadManifestAsync(CancellationToken cancellationToken = default);

    public OperationResult<long> CopyEntryToNewFile(
        string source,
        string destinationPath,
        CancellationToken cancellationToken = default);
}

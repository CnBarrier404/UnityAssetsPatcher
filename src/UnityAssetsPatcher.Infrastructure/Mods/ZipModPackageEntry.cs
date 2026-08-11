using System.IO.Compression;
using UnityAssetsPatcher.Application.Mods;

namespace UnityAssetsPatcher.Infrastructure.Mods;

internal sealed class ZipModPackageEntry : IModPackageEntry
{
    public string FullName => _entry.FullName;
    public string Name => _entry.Name;
    public long Length => _entry.Length;

    private readonly ZipArchiveEntry _entry;

    public ZipModPackageEntry(ZipArchiveEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        _entry = entry;
    }

    public Task<Stream> OpenReadAsync(CancellationToken cancellationToken = default)
    {
        return _entry.OpenAsync(cancellationToken);
    }
}

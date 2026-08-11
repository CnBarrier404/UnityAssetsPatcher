using System.IO.Compression;
using UnityAssetsPatcher.Application.Mods;

namespace UnityAssetsPatcher.Infrastructure.Mods;

internal sealed class ZipModPackageSession : IModPackageSession
{
    public IReadOnlyList<IModPackageEntry> Entries { get; }

    private readonly ZipArchive _archive;

    public ZipModPackageSession(ZipArchive archive)
    {
        ArgumentNullException.ThrowIfNull(archive);

        _archive = archive;
        Entries = [.. archive.Entries.Select(entry => new ZipModPackageEntry(entry))];
    }

    public void Dispose()
    {
        _archive.Dispose();
    }
}

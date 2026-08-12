using System.IO.Compression;
using UnityAssetsPatcher.Application.Mods;

namespace UnityAssetsPatcher.Infrastructure.Mods;

internal sealed class ZipModArchiveSession : IModArchiveSession
{
    public IReadOnlyList<IModArchiveEntry> Entries { get; }

    private readonly ZipArchive _archive;

    public ZipModArchiveSession(ZipArchive archive)
    {
        ArgumentNullException.ThrowIfNull(archive);

        _archive = archive;
        Entries = [.. archive.Entries.Select(entry => new ZipModArchiveEntry(entry))];
    }

    public void Dispose()
    {
        _archive.Dispose();
    }
}

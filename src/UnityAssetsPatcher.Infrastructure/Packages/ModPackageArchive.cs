using System.IO.Compression;
using Microsoft.Extensions.Logging;
using UnityAssetsPatcher.Application.Packages;

namespace UnityAssetsPatcher.Infrastructure.Packages;

internal sealed class ModPackageArchive : IModPackageArchive
{
    public string PackagePath { get; }

    public IReadOnlyList<PackageEntryInfo> Entries { get; }

    private readonly ZipArchive _archive;
    private readonly IReadOnlyList<ZipArchiveEntry> _archiveEntries;
    private readonly ILogger _logger;

    public ModPackageArchive(string packagePath, ZipArchive archive, ILogger logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);
        ArgumentNullException.ThrowIfNull(archive);
        ArgumentNullException.ThrowIfNull(logger);

        PackagePath = packagePath;
        _archive = archive;
        _logger = logger;
        _archiveEntries = Array.AsReadOnly([.. archive.Entries]);
        Entries = Array.AsReadOnly(
        [
            .. _archiveEntries.Select((entry, index) => new PackageEntryInfo(
                new PackageEntryId(index),
                entry.FullName,
                entry.Length,
                string.IsNullOrEmpty(entry.Name))),
        ]);
    }

    public Stream OpenEntry(PackageEntryId entryId)
    {
        if (entryId.Value < 0 || entryId.Value >= _archiveEntries.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(entryId));
        }

        ZipArchiveEntry entry = _archiveEntries[entryId.Value];

        ModPackageLog.OpeningEntry(_logger, entry.FullName, PackagePath);

        try
        {
            Stream stream = entry.Open();

            ModPackageLog.EntryOpened(_logger, entry.FullName, PackagePath);

            return stream;
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException)
        {
            ModPackageLog.EntryOpenFailed(_logger, entry.FullName, PackagePath, exception);

            throw;
        }
    }

    public void Dispose()
    {
        _archive.Dispose();
    }
}

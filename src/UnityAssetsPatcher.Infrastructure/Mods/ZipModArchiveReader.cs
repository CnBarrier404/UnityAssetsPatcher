using System.IO.Compression;
using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Application.Mods;

namespace UnityAssetsPatcher.Infrastructure.Mods;

public sealed class ZipModArchiveReader : IModArchiveReader
{
    private readonly IFileSystemOperations _fileSystemOperations;

    public ZipModArchiveReader(IFileSystemOperations fileSystemOperations)
    {
        ArgumentNullException.ThrowIfNull(fileSystemOperations);

        _fileSystemOperations = fileSystemOperations;
    }

    public async Task<IModArchiveSession> OpenAsync(string archivePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(archivePath);
        cancellationToken.ThrowIfCancellationRequested();

        string fullArchivePath = Path.GetFullPath(archivePath);
        Stream? stream = _fileSystemOperations.OpenRead(fullArchivePath);
        ZipArchive? archive = null;

        try
        {
            archive = await ZipArchive.CreateAsync(
                stream,
                ZipArchiveMode.Read,
                leaveOpen: false,
                entryNameEncoding: null,
                cancellationToken).ConfigureAwait(false);
            stream = null;

            var session = new ZipModArchiveSession(archive);
            archive = null;

            return session;
        }
        finally
        {
            if (archive is not null)
            {
                await archive.DisposeAsync().ConfigureAwait(false);
            }

            if (stream is not null)
            {
                await stream.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}

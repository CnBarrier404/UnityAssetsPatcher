using UnityAssetsPatcher.Application.Mods;

namespace UnityAssetsPatcher.Application.Tests.Mods;

internal sealed class StubModArchiveReader : IModArchiveReader
{
    public string? OpenedPath { get; private set; }

    private readonly Func<string, CancellationToken, Task<IModArchiveSession>> _openAsync;

    public StubModArchiveReader(IModArchiveSession session)
        : this(_ => session) { }

    public StubModArchiveReader(Func<string, IModArchiveSession> open)
    {
        ArgumentNullException.ThrowIfNull(open);

        _openAsync = (path, _) => Task.FromResult(open(path));
    }

    public async Task<IModArchiveSession> OpenAsync(
        string archivePath,
        CancellationToken cancellationToken = default)
    {
        OpenedPath = archivePath;

        return await _openAsync(archivePath, cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class StubModArchiveSession : IModArchiveSession
{
    public IReadOnlyList<IModArchiveEntry> Entries { get; }
    public bool IsDisposed { get; private set; }

    public StubModArchiveSession(byte[] manifestBytes, params (string Path, byte[] Contents)[] entries)
        : this(new StubModArchiveEntry("manifest.json", manifestBytes), entries) { }

    public StubModArchiveSession(
        byte[] manifestBytes,
        long declaredManifestLength,
        params (string Path, byte[] Contents)[] entries)
        : this(new StubModArchiveEntry("manifest.json", manifestBytes, declaredManifestLength), entries) { }

    private StubModArchiveSession(
        IModArchiveEntry manifestEntry,
        params (string Path, byte[] Contents)[] entries)
    {
        ArgumentNullException.ThrowIfNull(manifestEntry);

        Entries =
        [
            manifestEntry,
            .. entries.Select(entry => new StubModArchiveEntry(entry.Path, entry.Contents)),
        ];
    }

    public void Dispose()
    {
        IsDisposed = true;
    }
}

internal sealed class StubModArchiveEntry : IModArchiveEntry
{
    public string FullName { get; }
    public string Name => Path.GetFileName(FullName);
    public long Length { get; }

    private readonly byte[] _contents;

    public StubModArchiveEntry(string fullName, byte[] contents)
        : this(fullName, contents, contents.Length) { }

    public StubModArchiveEntry(string fullName, byte[] contents, long length)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);
        ArgumentNullException.ThrowIfNull(contents);
        ArgumentOutOfRangeException.ThrowIfNegative(length);

        FullName = fullName;
        _contents = contents;
        Length = length;
    }

    public Task<Stream> OpenReadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Stream stream = new MemoryStream(_contents, writable: false);

        return Task.FromResult(stream);
    }
}

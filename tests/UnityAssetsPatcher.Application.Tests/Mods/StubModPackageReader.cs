using UnityAssetsPatcher.Application.Mods;

namespace UnityAssetsPatcher.Application.Tests.Mods;

internal sealed class StubModPackageReader : IModPackageReader
{
    public string? OpenedPath { get; private set; }

    private readonly Func<string, CancellationToken, Task<IModPackageSession>> _openAsync;

    public StubModPackageReader(IModPackageSession session)
        : this(_ => session) { }

    public StubModPackageReader(Func<string, IModPackageSession> open)
    {
        ArgumentNullException.ThrowIfNull(open);

        _openAsync = (path, _) => Task.FromResult(open(path));
    }

    public async Task<IModPackageSession> OpenAsync(
        string archivePath,
        CancellationToken cancellationToken = default)
    {
        OpenedPath = archivePath;

        return await _openAsync(archivePath, cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class StubModPackageSession : IModPackageSession
{
    public IReadOnlyList<IModPackageEntry> Entries { get; }
    public bool IsDisposed { get; private set; }

    public StubModPackageSession(byte[] manifestBytes, params (string Path, byte[] Contents)[] entries)
        : this(new StubModPackageEntry("manifest.json", manifestBytes), entries) { }

    public StubModPackageSession(
        byte[] manifestBytes,
        long declaredManifestLength,
        params (string Path, byte[] Contents)[] entries)
        : this(new StubModPackageEntry("manifest.json", manifestBytes, declaredManifestLength), entries) { }

    private StubModPackageSession(
        IModPackageEntry manifestEntry,
        params (string Path, byte[] Contents)[] entries)
    {
        ArgumentNullException.ThrowIfNull(manifestEntry);

        Entries =
        [
            manifestEntry,
            .. entries.Select(entry => new StubModPackageEntry(entry.Path, entry.Contents)),
        ];
    }

    public void Dispose()
    {
        IsDisposed = true;
    }
}

internal sealed class StubModPackageEntry : IModPackageEntry
{
    public string FullName { get; }
    public string Name => Path.GetFileName(FullName);
    public long Length { get; }

    private readonly byte[] _contents;

    public StubModPackageEntry(string fullName, byte[] contents)
        : this(fullName, contents, contents.Length) { }

    public StubModPackageEntry(string fullName, byte[] contents, long length)
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
